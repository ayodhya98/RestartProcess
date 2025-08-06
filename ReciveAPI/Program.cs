using Microsoft.AspNetCore.Http.Features;
using OpenTelemetry.Exporter;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using ReciveAPI.Services;
using ReciveAPI.Services.IServices;
using System.Runtime.InteropServices;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers()
    .AddNewtonsoftJson();

builder.Services.AddOpenTelemetry()
    .WithMetrics(metrics => metrics
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation()
        .AddRuntimeInstrumentation()
        .AddProcessInstrumentation()
        .AddOtlpExporter(otlp =>
        {
            otlp.Endpoint = new Uri("http://aspire-dashboard:4317");
            otlp.Protocol = OtlpExportProtocol.Grpc;
        }))
    .WithTracing(tracing => tracing
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation()
        .AddSource("RabbitMQService")
        .AddSource("FileProcessingQueueServices")
        .AddSource("FileProcessingBackgroundService")
        .SetSampler(new AlwaysOnSampler())
        .AddOtlpExporter(otlp =>
        {
            otlp.Endpoint = new Uri("http://aspire-dashboard:4317");
            otlp.Protocol = OtlpExportProtocol.Grpc;
        }));
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddDebug();
builder.Logging.SetMinimumLevel(LogLevel.Information);

builder.Logging.AddOpenTelemetry(options =>
{
    options.IncludeFormattedMessage = true;
    options.IncludeScopes = true;
    options.ParseStateValues = true;
    options.SetResourceBuilder(ResourceBuilder.CreateDefault()
        .AddService("reciveapi")
        .AddAttributes(new Dictionary<string, object>
        {
            ["deployment.environment"] = builder.Environment.EnvironmentName
        }));
    options.AddOtlpExporter(otlp =>
    {
        otlp.Endpoint = new Uri("http://aspire-dashboard:18889");
        otlp.Protocol = OtlpExportProtocol.Grpc;
        otlp.ExportProcessorType = OpenTelemetry.ExportProcessorType.Batch;
    });
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
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

var logger = app.Services.GetRequiredService<ILogger<Program>>();
logger.LogInformation("ReciveAPI application starting up at {Time}", DateTimeOffset.Now);
logger.LogInformation("Application environment: {Environment}", app.Environment.EnvironmentName);
logger.LogWarning("This is a test warning message from ReciveAPI");
logger.LogInformation("ReciveAPI application startup completed successfully");

app.Run();
