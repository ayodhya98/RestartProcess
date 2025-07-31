using BackgroundProcessWorker;
using BackgroundProcessWorker.Services;
using BackgroundProcessWorker.Services.IServices;

//var builder = Host.CreateApplicationBuilder(args);
//builder.Services.AddHostedService<Worker>();

////var host = builder.Build();
////host.Run();

IHost host = Host.CreateDefaultBuilder(args)
    .ConfigureServices(services =>
    {
        services.AddHostedService<Worker>();
        services.AddSingleton<IRabbitMQService,RabbitMQService>();
    })
    .Build();

await host.RunAsync();
