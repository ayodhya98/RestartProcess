# Test script to generate logs for Aspire dashboard
Write-Host "Testing ReciveAPI to generate logs..." -ForegroundColor Green

# Wait for services to start
Start-Sleep -Seconds 10

# Test the API endpoint
try {
    $response = Invoke-RestMethod -Uri "http://localhost:8080/api/tracking/process-file" -Method POST -ContentType "multipart/form-data" -Form @{
        file = [System.IO.File]::OpenRead("test.json")
    }
    Write-Host "API Response: $($response | ConvertTo-Json)" -ForegroundColor Yellow
} catch {
    Write-Host "API Error: $($_.Exception.Message)" -ForegroundColor Red
}

Write-Host "Test completed. Check Aspire dashboard at http://localhost:18888" -ForegroundColor Green 