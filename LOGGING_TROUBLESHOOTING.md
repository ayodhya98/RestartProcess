# Aspire Dashboard Logging Troubleshooting Guide

## Issues Fixed

1. **Missing OpenTelemetry.Extensions.Logging Package**: Added the required package to both projects
2. **Incorrect OTLP Endpoint**: Removed trailing slash from endpoint URLs
3. **Improved Logging Configuration**: Added proper console logging and log level configuration
4. **Enhanced Docker Logging**: Added logging configuration to docker-compose.yml
5. **Additional Environment Variables**: Added OTEL_* environment variables for better integration

## Changes Made

### 1. Project Files
- Added `OpenTelemetry.Extensions.Logging` package to both `ReciveAPI.csproj` and `BackgroundProcessWorker.csproj`

### 2. Program.cs Files
- Fixed OTLP endpoint URLs (removed trailing slash)
- Added proper logging configuration with console and debug providers
- Set minimum log level to Information
- Added more startup logging statements

### 3. Worker.cs
- Added more detailed logging statements
- Added timestamps to log messages
- Added activity ID logging

### 4. Docker Compose
- Added logging configuration for all services
- Added OTEL environment variables for logs, traces, and metrics
- Configured log rotation (10MB max size, 3 files)

## How to Test

1. **Rebuild and restart the containers**:
   ```bash
   docker-compose down
   docker-compose build --no-cache
   docker-compose up -d
   ```

2. **Check container logs**:
   ```bash
   docker-compose logs reciveapi
   docker-compose logs backgroundworker
   ```

3. **Test the API** (using the provided test script):
   ```powershell
   .\test-logs.ps1
   ```

4. **Access Aspire Dashboard**:
   - Open http://localhost:18888 in your browser
   - Navigate to the "Logs" section
   - You should now see logs from both services

## Expected Logs

### ReciveAPI Startup Logs:
- "ReciveAPI application starting up at {Time}"
- "Application environment: Development"
- "This is a test warning message from ReciveAPI"
- "ReciveAPI application startup completed successfully"

### Background Worker Logs:
- "Background Worker service starting up at {Time}"
- "Worker started listening for messages..."
- "Activity started with ID: {ActivityId}"
- "Background Worker service initialized successfully"

### API Request Logs:
- File upload validation logs
- File processing logs
- Error logs (if any)

## Troubleshooting Steps

1. **Check if containers are running**:
   ```bash
   docker-compose ps
   ```

2. **Check individual service logs**:
   ```bash
   docker-compose logs aspire-dashboard
   docker-compose logs reciveapi
   docker-compose logs backgroundworker
   ```

3. **Verify OpenTelemetry connectivity**:
   - Check if the aspire-dashboard is accessible at http://localhost:18888
   - Verify that services can reach the dashboard on port 4317

4. **Check environment variables**:
   ```bash
   docker-compose exec reciveapi env | grep OTEL
   docker-compose exec backgroundworker env | grep OTEL
   ```

## Common Issues

1. **No logs in dashboard**: Ensure the OTEL_EXPORTER_OTLP_ENDPOINT is correct
2. **Connection refused**: Check if aspire-dashboard is running and healthy
3. **Missing packages**: Ensure all OpenTelemetry packages are installed
4. **Log level too high**: Verify minimum log level is set to Information or lower

## Additional Notes

- The Aspire dashboard may take a few minutes to start collecting logs
- Logs are sent in batches, so there might be a delay
- Make sure to trigger some activity (API calls, background processing) to generate logs
- Check both the "Logs" and "Traces" sections in the Aspire dashboard 