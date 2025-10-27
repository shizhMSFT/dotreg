#!/usr/bin/env pwsh
# Complete E2E testing workflow for dotreg with LocalStack and ORAS

param(
    [switch]$SkipLocalStack = $false,
    [switch]$KeepRunning = $false
)

$ErrorActionPreference = "Stop"
$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$projectRoot = Split-Path -Parent $scriptDir

Write-Host "==================================================" -ForegroundColor Cyan
Write-Host "  dotreg Complete E2E Testing" -ForegroundColor Cyan
Write-Host "==================================================" -ForegroundColor Cyan
Write-Host ""

# Step 1: Start LocalStack
if (-not $SkipLocalStack) {
    Write-Host "STEP 1: Starting LocalStack..." -ForegroundColor Yellow
    & "$scriptDir\start-localstack.ps1"
    if ($LASTEXITCODE -ne 0) {
        Write-Host "❌ Failed to start LocalStack" -ForegroundColor Red
        exit 1
    }
} else {
    Write-Host "STEP 1: Skipping LocalStack startup (assuming it's already running)" -ForegroundColor Yellow
}

Write-Host ""

# Step 2: Start dotreg registry
Write-Host "STEP 2: Starting dotreg registry..." -ForegroundColor Yellow
$registryJob = Start-Job -ScriptBlock {
    Set-Location $using:projectRoot
    cd src/Dotreg.Api
    $env:ASPNETCORE_ENVIRONMENT = "Development"
    dotnet run
}

Write-Host "Waiting for registry to start..." -ForegroundColor Gray
Start-Sleep -Seconds 8

# Check if registry is ready
$registryReady = $false
for ($i = 0; $i -lt 10; $i++) {
    try {
        $response = Invoke-WebRequest -Uri "http://localhost:5153/v2/" -UseBasicParsing -TimeoutSec 2 -ErrorAction Stop
        if ($response.StatusCode -eq 200) {
            $registryReady = $true
            Write-Host "✅ Registry is ready on http://localhost:5153" -ForegroundColor Green
            break
        }
    }
    catch {
        Write-Host "Waiting... ($($i+1)/10)" -ForegroundColor Gray
        Start-Sleep -Seconds 2
    }
}

if (-not $registryReady) {
    Write-Host "❌ Registry failed to start" -ForegroundColor Red
    Write-Host "Job output:" -ForegroundColor Yellow
    Receive-Job -Job $registryJob
    Stop-Job -Job $registryJob
    Remove-Job -Job $registryJob
    exit 1
}

Write-Host ""

# Step 3: Run ORAS tests
Write-Host "STEP 3: Running ORAS E2E tests..." -ForegroundColor Yellow
Write-Host ""
& pwsh "$scriptDir\run-oras-tests.ps1"
$testExitCode = $LASTEXITCODE

Write-Host ""

# Cleanup
if (-not $KeepRunning) {
    Write-Host "CLEANUP: Stopping registry..." -ForegroundColor Yellow
    Stop-Job -Job $registryJob
    Remove-Job -Job $registryJob
    
    if (-not $SkipLocalStack) {
        Write-Host "CLEANUP: Stopping LocalStack..." -ForegroundColor Yellow
        docker stop dotreg-localstack | Out-Null
        docker rm dotreg-localstack | Out-Null
    }
    
    Write-Host "✅ Cleanup complete" -ForegroundColor Green
} else {
    Write-Host "⚠️  Registry is still running (Job ID: $($registryJob.Id))" -ForegroundColor Yellow
    Write-Host "   To stop: Stop-Job -Id $($registryJob.Id); Remove-Job -Id $($registryJob.Id)" -ForegroundColor Gray
    Write-Host "   LocalStack is still running: docker stop dotreg-localstack" -ForegroundColor Gray
}

Write-Host ""

# Exit with test result
exit $testExitCode
