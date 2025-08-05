using BackgroundProcessWorker;
using BackgroundProcessWorker.Services;
using BackgroundProcessWorker.Services.IServices;
using OpenTelemetry.Exporter;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

IHost host = Host.CreateDefaultBuilder(args)
    .ConfigureServices(services =>
    {
        // Configure OpenTelemetry
        services.AddOpenTelemetry()
            .WithMetrics(metrics => metrics
                .AddRuntimeInstrumentation()
                .AddProcessInstrumentation()
                .AddMeter("BackgroundProcessWorker.RabbitMQ")
                .AddOtlpExporter(o =>
                {
                    o.Endpoint = new Uri("http://aspire-dashboard:18889");
                    o.Protocol = OtlpExportProtocol.HttpProtobuf;
                }))
            .WithTracing(tracing => tracing
                .AddSource("RabbitMQService")
                .AddSource("FileProcessingBackgroundService")
                .AddOtlpExporter(o =>
                {
                    o.Endpoint = new Uri("http://aspire-dashboard:18889");
                    o.Protocol = OtlpExportProtocol.HttpProtobuf;
                }));

        // Configure logging
        services.AddLogging(logging =>
        {
            logging.AddOpenTelemetry(options =>
            {
                options.IncludeFormattedMessage = true;
                options.IncludeScopes = true;
                options.SetResourceBuilder(
                    ResourceBuilder.CreateDefault()
                        .AddService("BackgroundWorker")
                        .AddAttributes(new Dictionary<string, object>
                        {
                            ["deployment.environment"] = "Development"
                        }));

                options.AddOtlpExporter(exporter =>
                {
                    exporter.Endpoint = new Uri("http://aspire-dashboard:18889");
                    exporter.Protocol = OtlpExportProtocol.HttpProtobuf;
                });
            });

            logging.AddConsole();
        });

        // Add application services
        services.AddHostedService<Worker>();
        services.AddSingleton<IRabbitMQService, RabbitMQService>();
    })
    .Build();

await host.RunAsync();