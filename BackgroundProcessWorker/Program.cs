using BackgroundProcessWorker;
using BackgroundProcessWorker.Services;
using BackgroundProcessWorker.Services.IServices;
using OpenTelemetry.Exporter;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

IHost host = Host.CreateDefaultBuilder(args)
    .ConfigureServices((hostContext, services) =>
    {
        var resourceBuilder = ResourceBuilder.CreateDefault()
            .AddService("backgroundworker")
            .AddAttributes(new Dictionary<string, object>
            {
                ["deployment.environment"] = hostContext.HostingEnvironment.EnvironmentName
            });

        services.AddOpenTelemetry()
            .WithMetrics(metrics => metrics
                .SetResourceBuilder(resourceBuilder)
                .AddRuntimeInstrumentation()
                .AddProcessInstrumentation()
                .AddMeter("BackgroundProcessWorker.RabbitMQ")
                .AddOtlpExporter(otlp =>
                {
                    otlp.Endpoint = new Uri("http://aspire-dashboard:4317/");
                    otlp.Protocol = OtlpExportProtocol.HttpProtobuf;
                }))
            .WithTracing(tracing => tracing
                .SetResourceBuilder(resourceBuilder)
                .AddSource("RabbitMQService")
                .AddSource("FileProcessingBackgroundService")
                .SetSampler(new AlwaysOnSampler())
                .AddOtlpExporter(otlp =>
                {
                    otlp.Endpoint = new Uri("http://aspire-dashboard:4317/");
                    otlp.Protocol = OtlpExportProtocol.HttpProtobuf;
                }));

        services.AddLogging(logging =>
        {
            logging.AddOpenTelemetry(options =>
            {
                options.IncludeFormattedMessage = true;
                options.IncludeScopes = true;
                options.SetResourceBuilder(resourceBuilder);
                options.AddOtlpExporter(otlp =>
                {
                    otlp.Endpoint = new Uri("http://aspire-dashboard:4317/");
                    otlp.Protocol = OtlpExportProtocol.HttpProtobuf;
                });
            });
            logging.AddConsole();
        });

        services.AddHostedService<Worker>();
        services.AddSingleton<IRabbitMQService, RabbitMQService>();
    })
    .Build();

await host.RunAsync();
