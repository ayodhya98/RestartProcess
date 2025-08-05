using BackgroundProcessWorker;
using BackgroundProcessWorker.Services;
using BackgroundProcessWorker.Services.IServices;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

IHost host = Host.CreateDefaultBuilder(args)
    .ConfigureServices(services =>
    {
        services.AddOpenTelemetry()
            .ConfigureResource(resource => resource
                .AddService(serviceName: "BackgroundWorker")
                .AddAttributes(new Dictionary<string, object>
                {
                    ["deployment.environment"] = "Development"
                }))
            .WithMetrics(metrics => metrics
                .AddRuntimeInstrumentation()
                .AddProcessInstrumentation()
                .AddMeter("BackgroundProcessWorker.RabbitMQ")
                .AddOtlpExporter())
            .WithTracing(tracing => tracing
                .AddSource("RabbitMQService")
                .AddSource("FileProcessingBackgroundService")
                .AddOtlpExporter());

        services.AddLogging(logging =>
        {
            logging.AddOpenTelemetry(options =>
            {
                options.IncludeFormattedMessage = true;
                options.IncludeScopes = true;
                options.SetResourceBuilder(ResourceBuilder.CreateDefault()
                    .AddService("BackgroundWorker")
                    .AddAttributes(new Dictionary<string, object>
                    {
                        ["deployment.environment"] = "Development"
                    }));
                options.AddOtlpExporter();
            });
        });
       
        services.AddHostedService<Worker>();
        services.AddSingleton<IRabbitMQService, RabbitMQService>();

    })
    .Build();

await host.RunAsync();
