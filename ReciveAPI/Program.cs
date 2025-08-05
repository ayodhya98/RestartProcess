using Microsoft.AspNetCore.Http.Features;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using ReciveAPI.Services;
using ReciveAPI.Services.IServices;
using System.Runtime.InteropServices;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers()
    .AddNewtonsoftJson();

builder.Services.AddOpenTelemetry()
    .ConfigureResource(resource => resource.AddService(serviceName: "ReciveAPI"))
    .WithMetrics(metrics =>
    {
        metrics
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation();
        metrics.AddOtlpExporter();

    }
                 )
    .WithTracing(tracing => tracing
                 .AddAspNetCoreInstrumentation()
                 .AddHttpClientInstrumentation()
                 .AddSource("RabbitMQService")
                 .AddSource("FileProcessingQueueServices")
                 .AddSource("FileProcessingBackgroundService")
                 .AddOtlpExporter()
                 //.AddOtlpExporter(options =>
                 //{
                 //    options.Endpoint = new Uri("http://localhost:4317");
                 //})
                 );

builder.Logging.AddOpenTelemetry(logging =>
{
    logging.IncludeFormattedMessage = true;
    logging.IncludeScopes = true;
    logging.AddConsoleExporter();
    logging.AddOtlpExporter();
});


// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddSingleton<IFileProcessingQueueServices, FileProcessingQueueServices>();
builder.Services.AddSingleton<IRabbitMQService, RabbitMQService>();
builder.Services.AddHostedService<FileProcessingBackgroundService>();



builder.WebHost.ConfigureKestrel(serverOptions =>
{
    serverOptions.Limits.MaxRequestBodySize = 524288000; // 500MB
});

// Configure form options
builder.Services.Configure<FormOptions>(options =>
{
    options.MultipartBodyLengthLimit = 524288000; // 500MB
    options.MemoryBufferThreshold = 524288000; // 500MB
    options.ValueLengthLimit = int.MaxValue;
    options.ValueCountLimit = int.MaxValue;
});


if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
{
    AppContext.SetSwitch("Switch.System.IO.UseLegacyPathHandling", false);
    AppContext.SetSwitch("Switch.System.IO.BlockLongPaths", false);
}

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "ReciveAPI v1");
        c.RoutePrefix = string.Empty;  // Serve Swagger UI at app root (http://localhost:8080/)
    });
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();
