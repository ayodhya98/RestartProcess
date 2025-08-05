using BackgroundProcessWorker;
using BackgroundProcessWorker.Services;
using BackgroundProcessWorker.Services.IServices;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

//var builder = Host.CreateApplicationBuilder(args);
//builder.Services.AddHostedService<Worker>();

////var host = builder.Build();
////host.Run();



IHost host = Host.CreateDefaultBuilder(args)
    .ConfigureServices(services =>
    {
        services.AddOpenTelemetry()
        .ConfigureResource(resource => resource.AddService(serviceName: "WorkerService"))
        .WithMetrics(matrices =>
        {
            matrices.AddAspNetCoreInstrumentation()
                    .AddAspNetCoreInstrumentation();
            matrices.AddOtlpExporter();
        }

                 )
        .WithTracing(tracing =>
        tracing.AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation()
                .AddSource("RabbitMQService")
                .AddOtlpExporter()

            );
        services.AddLogging(logging =>
        {
            logging.AddOpenTelemetry(options =>
            {
                options.IncludeFormattedMessage = true;
                options.IncludeScopes = true;
                options.AddOtlpExporter();
            });
        });

        services.AddHostedService<Worker>();
        services.AddSingleton<IRabbitMQService, RabbitMQService>();

    })
    .Build();

await host.RunAsync();
