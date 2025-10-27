#!/usr/bin/env pwsh
# Start LocalStack for dotreg E2E testing

$ErrorActionPreference = "Stop"

Write-Host "Starting LocalStack container..." -ForegroundColor Cyan

# Check if Docker is running
try {
    docker ps | Out-Null
}
catch {
    Write-Host "❌ Docker is not running. Please start Docker Desktop first." -ForegroundColor Red
    exit 1
}

# Stop and remove existing LocalStack container if it exists
Write-Host "Cleaning up existing LocalStack container..." -ForegroundColor Yellow
docker stop dotreg-localstack 2>$null | Out-Null
docker rm dotreg-localstack 2>$null | Out-Null

# Start LocalStack
Write-Host "Starting LocalStack on port 4566..." -ForegroundColor Cyan
docker run -d `
    --name dotreg-localstack `
    -p 4566:4566 `
    -e SERVICES=s3 `
    -e DEBUG=1 `
    localstack/localstack:latest

if ($LASTEXITCODE -ne 0) {
    Write-Host "❌ Failed to start LocalStack" -ForegroundColor Red
    exit 1
}

# Wait for LocalStack to be ready
Write-Host "Waiting for LocalStack to be ready..." -ForegroundColor Yellow
$retries = 30
$ready = $false

for ($i = 0; $i -lt $retries; $i++) {
    try {
        $response = Invoke-WebRequest -Uri "http://localhost:4566/_localstack/health" -UseBasicParsing -TimeoutSec 2 -ErrorAction Stop
        $health = $response.Content | ConvertFrom-Json
        
        if ($health.services.s3 -eq "available" -or $health.services.s3 -eq "running") {
            $ready = $true
            Write-Host "✅ LocalStack is ready!" -ForegroundColor Green
            break
        }
    }
    catch {
        # Ignore errors and retry
    }
    
    Write-Host "Waiting... ($($i+1)/$retries)" -ForegroundColor Gray
    Start-Sleep -Seconds 2
}

if (-not $ready) {
    Write-Host "❌ LocalStack did not become ready in time" -ForegroundColor Red
    Write-Host "Container logs:" -ForegroundColor Yellow
    docker logs dotreg-localstack
    exit 1
}

# Create the S3 bucket
Write-Host "Creating S3 bucket 'dotreg'..." -ForegroundColor Cyan
docker exec dotreg-localstack awslocal s3 mb s3://dotreg 2>$null

Write-Host ""
Write-Host "✅ LocalStack is ready for E2E testing!" -ForegroundColor Green
Write-Host "   S3 Endpoint: http://localhost:4566" -ForegroundColor Gray
Write-Host "   Bucket: dotreg" -ForegroundColor Gray
Write-Host ""
Write-Host "To stop LocalStack, run:" -ForegroundColor Cyan
Write-Host "   docker stop dotreg-localstack" -ForegroundColor Gray
Write-Host ""
