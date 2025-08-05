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

// Add services to the container
builder.Services.AddControllers().AddNewtonsoftJson();

// Configure OpenTelemetry with simplified setup
builder.Services.AddOpenTelemetry()
    .WithMetrics(metrics => metrics
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation()
        .AddRuntimeInstrumentation()
        .AddProcessInstrumentation()
        .AddMeter("Microsoft.AspNetCore.Hosting")
        .AddOtlpExporter(o =>
        {
            o.Endpoint = new Uri("http://aspire-dashboard:18889");
            o.Protocol = OtlpExportProtocol.HttpProtobuf;
        }))
    .WithTracing(tracing => tracing
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation()
        .AddSource("RabbitMQService")
        .AddSource("FileProcessingQueueServices")
        .AddOtlpExporter(o =>
        {
            o.Endpoint = new Uri("http://aspire-dashboard:18889");
            o.Protocol = OtlpExportProtocol.HttpProtobuf;
        }));

// Configure logging
builder.Logging.AddOpenTelemetry(options =>
{
    options.IncludeFormattedMessage = true;
    options.IncludeScopes = true;
    options.SetResourceBuilder(
        ResourceBuilder.CreateDefault()
            .AddService("ReciveAPI")
            .AddAttributes(new Dictionary<string, object>
            {
                ["deployment.environment"] = builder.Environment.EnvironmentName
            }));

    options.AddOtlpExporter(exporter =>
    {
        exporter.Endpoint = new Uri("http://aspire-dashboard:18889");
        exporter.Protocol = OtlpExportProtocol.HttpProtobuf;
    });
});

// Add application services
builder.Services.AddSingleton<IFileProcessingQueueServices, FileProcessingQueueServices>();
builder.Services.AddSingleton<IRabbitMQService, RabbitMQService>();
builder.Services.AddHostedService<FileProcessingBackgroundService>();

// Configure Kestrel
builder.WebHost.ConfigureKestrel(serverOptions =>
{
    serverOptions.Limits.MaxRequestBodySize = 524_288_000; // 500MB
});

// Configure Form Options
builder.Services.Configure<FormOptions>(options =>
{
    options.MultipartBodyLengthLimit = 524_288_000; // 500MB
    options.ValueLengthLimit = int.MaxValue;
});

// Fix path issues on Windows
if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
{
    AppContext.SetSwitch("Switch.System.IO.UseLegacyPathHandling", false);
    AppContext.SetSwitch("Switch.System.IO.BlockLongPaths", false);
}

var app = builder.Build();

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "ReciveAPI v1");
        c.RoutePrefix = string.Empty;
    });
}

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();
app.Run();