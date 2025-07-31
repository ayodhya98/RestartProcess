using Microsoft.AspNetCore.Http.Features;
using ReciveAPI.Services;
using ReciveAPI.Services.IServices;
using System.Runtime.InteropServices;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers()
    .AddNewtonsoftJson();

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
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();
