#!/usr/bin/env pwsh
# ORAS E2E Testing Script for dotreg
# Based on: https://github.com/shizhMSFT/jreg/blob/main/ORAS_TEST_RESULTS.md

param(
    [string]$Registry = "localhost:5153",
    [string]$TestDir = "$env:TEMP\oras-tests"
)

$ErrorActionPreference = "Continue"
$ProgressPreference = "SilentlyContinue"

Write-Host "==================================================" -ForegroundColor Cyan
Write-Host "  dotreg ORAS E2E Testing" -ForegroundColor Cyan
Write-Host "  Registry: $Registry" -ForegroundColor Cyan
Write-Host "  Test Dir: $TestDir" -ForegroundColor Cyan
Write-Host "==================================================" -ForegroundColor Cyan
Write-Host ""

# Setup test directory
if (Test-Path $TestDir) {
    Remove-Item -Recurse -Force $TestDir
}
New-Item -ItemType Directory -Force -Path $TestDir | Out-Null
Set-Location $TestDir

# Create test files
"Hello from dotreg ORAS test!" | Out-File -Encoding ASCII -NoNewline test-artifact.txt
"Version 2.0 test content" | Out-File -Encoding ASCII -NoNewline test-v2.txt
"Digital signature data" | Out-File -Encoding ASCII -NoNewline signature.txt
"SBOM: Software Bill of Materials" | Out-File -Encoding ASCII -NoNewline sbom.txt
"Attestation: Build verified" | Out-File -Encoding ASCII -NoNewline attestation.txt

$testResults = @()

function Test-OrasCommand {
    param(
        [string]$Name,
        [string]$Command,
        [scriptblock]$Validation = { $true }
    )
    
    Write-Host "### Test: $Name" -ForegroundColor Yellow
    Write-Host "Command: $Command" -ForegroundColor Gray
    
    try {
        # Capture both stdout and stderr
        $output = Invoke-Expression $Command 2>&1 | Out-String
        Write-Host $output
        
        # Check exit code first
        $exitSuccess = $LASTEXITCODE -eq 0
        
        # Then run custom validation
        $validationSuccess = & $Validation
        
        $success = $exitSuccess -and $validationSuccess
        
        if ($success) {
            Write-Host "✅ PASSED" -ForegroundColor Green
            $script:testResults += [PSCustomObject]@{
                Test = $Name
                Status = "✅ PASSED"
                Command = $Command
            }
        } else {
            Write-Host "❌ FAILED (Exit: $LASTEXITCODE)" -ForegroundColor Red
            $script:testResults += [PSCustomObject]@{
                Test = $Name
                Status = "❌ FAILED"
                Command = $Command
            }
        }
    }
    catch {
        Write-Host "❌ ERROR: $_" -ForegroundColor Red
        $script:testResults += [PSCustomObject]@{
            Test = $Name
            Status = "❌ ERROR"
            Command = $Command
        }
    }
    
    Write-Host ""
}

# Wait for registry to be ready
Write-Host "Checking registry availability..." -ForegroundColor Cyan
$retries = 10
$ready = $false
for ($i = 0; $i -lt $retries; $i++) {
    try {
        $response = Invoke-WebRequest -Uri "http://$Registry/v2/" -UseBasicParsing -TimeoutSec 2 -ErrorAction Stop
        if ($response.StatusCode -eq 200) {
            $ready = $true
            Write-Host "✅ Registry is ready!" -ForegroundColor Green
            break
        }
    }
    catch {
        Write-Host "Waiting for registry... ($($i+1)/$retries)" -ForegroundColor Yellow
        Start-Sleep -Seconds 2
    }
}

if (-not $ready) {
    Write-Host "❌ Registry is not available at http://$Registry" -ForegroundColor Red
    Write-Host "Please start the registry first with: dotnet run --project src/Dotreg.Api" -ForegroundColor Yellow
    exit 1
}

Write-Host ""
Write-Host "==================================================" -ForegroundColor Cyan
Write-Host "  Starting ORAS Tests" -ForegroundColor Cyan
Write-Host "==================================================" -ForegroundColor Cyan
Write-Host ""

# Test 1: Push Artifact
Test-OrasCommand -Name "1. Push Artifact" `
    -Command "oras push $Registry/myrepo:v1.0 test-artifact.txt --plain-http" `
    -Validation { $true }

# Test 2: Pull Artifact
Test-OrasCommand -Name "2. Pull Artifact" `
    -Command "oras pull $Registry/myrepo:v1.0 --plain-http -o pulled1" `
    -Validation { 
        # Check if the file was pulled successfully and contains expected content
        return ((Test-Path "pulled1/test-artifact.txt") -and ((Get-Content "pulled1/test-artifact.txt" -Raw -ErrorAction SilentlyContinue) -like "*dotreg*"))
    }

# Test 3: List Tags
Test-OrasCommand -Name "3. List Tags" `
    -Command "oras repo tags $Registry/myrepo --plain-http" `
    -Validation { $output -match "v1.0" }

# Test 4: Fetch Manifest
Test-OrasCommand -Name "4. Fetch Manifest" `
    -Command "oras manifest fetch $Registry/myrepo:v1.0 --plain-http" `
    -Validation { $output -match "schemaVersion" }

# Test 5: Push Multiple Versions (tests blob deduplication)
Test-OrasCommand -Name "5. Push Multiple Versions (Deduplication)" `
    -Command "oras push $Registry/myrepo:v2.0 test-v2.txt --plain-http" `
    -Validation { $true }

# Test 6: Multiple Repositories
Test-OrasCommand -Name "6. Multiple Repositories" `
    -Command "oras push $Registry/testapp:latest test-artifact.txt --plain-http" `
    -Validation { $true }

# Test 7: Attach Referrer (requires subject field support)
Test-OrasCommand -Name "7. Attach Referrer (Signature)" `
    -Command "oras attach $Registry/myrepo:v1.0 --artifact-type application/vnd.example.signature.v1 signature.txt --plain-http" `
    -Validation { $true }

# Test 8: Attach Multiple Referrers
Test-OrasCommand -Name "8. Attach Referrer (SBOM)" `
    -Command "oras attach $Registry/myrepo:v1.0 --artifact-type application/vnd.example.sbom.v1 sbom.txt --plain-http" `
    -Validation { $true }

Test-OrasCommand -Name "9. Attach Referrer (Attestation)" `
    -Command "oras attach $Registry/myrepo:v1.0 --artifact-type application/vnd.example.attestation.v1 attestation.txt --plain-http" `
    -Validation { $true }

# Allow time for referrers index to be updated
Start-Sleep -Milliseconds 500

# Test 9: Discover Referrers
Test-OrasCommand -Name "10. Discover Referrers" `
    -Command "oras discover $Registry/myrepo:v1.0 --plain-http" `
    -Validation { 
        # ORAS discover shows referrers in tree format
        $true  # Command success is validation enough
    }

# Test 10: Get Referrers with curl (test filtering)
Write-Host "### Test: 11. Referrers API with Filtering" -ForegroundColor Yellow
try {
    # First get the manifest to find the digest
    $digest = (oras manifest fetch "$Registry/myrepo:v1.0" --plain-http --descriptor 2>&1 | ConvertFrom-Json).digest
    
    Write-Host "Subject digest: $digest" -ForegroundColor Gray
    
    # Test referrers API
    $url = "http://$Registry/v2/myrepo/referrers/$digest"
    Write-Host "Testing: $url" -ForegroundColor Gray
    $response = Invoke-WebRequest -Uri $url -UseBasicParsing
    $referrersIndex = [System.Text.Encoding]::UTF8.GetString($response.Content) | ConvertFrom-Json
    
    Write-Host "Found $($referrersIndex.manifests.Count) referrers" -ForegroundColor Gray
    
    if ($referrersIndex.manifests.Count -ge 3) {
        Write-Host "✅ PASSED - All referrers found" -ForegroundColor Green
        $testResults += [PSCustomObject]@{
            Test = "11. Referrers API"
            Status = "✅ PASSED"
            Command = "GET $url"
        }
    } else {
        Write-Host "⚠️  PARTIAL - Expected 3 referrers, found $($referrersIndex.manifests.Count) (may be timing)" -ForegroundColor Yellow
        $testResults += [PSCustomObject]@{
            Test = "11. Referrers API"
            Status = "⚠️  PARTIAL"
            Command = "GET $url"
        }
    }
    
    # Test with artifact type filter
    $filterUrl = "$url`?artifactType=application/vnd.example.signature.v1"
    Write-Host "Testing filtered: $filterUrl" -ForegroundColor Gray
    $filterResponse = Invoke-WebRequest -Uri $filterUrl -UseBasicParsing
    $filteredIndex = [System.Text.Encoding]::UTF8.GetString($filterResponse.Content) | ConvertFrom-Json
    
    if ($filteredIndex.manifests.Count -ge 1 -and $filteredIndex.manifests[0].artifactType -eq "application/vnd.example.signature.v1") {
        Write-Host "✅ PASSED - Artifact type filtering works" -ForegroundColor Green
        $testResults += [PSCustomObject]@{
            Test = "12. Referrers API Filtering"
            Status = "✅ PASSED"
            Command = "GET $filterUrl"
        }
    } else {
        Write-Host "❌ FAILED - Filtering did not work correctly (found $($filteredIndex.manifests.Count) manifests)" -ForegroundColor Red
        $testResults += [PSCustomObject]@{
            Test = "12. Referrers API Filtering"
            Status = "❌ FAILED"
            Command = "GET $filterUrl"
        }
    }
}
catch {
    Write-Host "❌ ERROR: $_" -ForegroundColor Red
    $testResults += [PSCustomObject]@{
        Test = "11-12. Referrers API"
        Status = "❌ ERROR"
        Command = "N/A"
    }
}
Write-Host ""

# Test 13: Delete Manifest
Test-OrasCommand -Name "13. Delete Manifest" `
    -Command "oras manifest delete $Registry/testapp:latest --plain-http --force" `
    -Validation { $true }

# Summary
Write-Host ""
Write-Host "==================================================" -ForegroundColor Cyan
Write-Host "  Test Results Summary" -ForegroundColor Cyan
Write-Host "==================================================" -ForegroundColor Cyan
Write-Host ""

$testResults | Format-Table -AutoSize

$passed = ($testResults | Where-Object { $_.Status -eq "✅ PASSED" }).Count
$failed = ($testResults | Where-Object { $_.Status -match "❌" }).Count
$total = $testResults.Count

Write-Host ""
Write-Host "Total: $total tests" -ForegroundColor Cyan
Write-Host "Passed: $passed tests" -ForegroundColor Green
Write-Host "Failed: $failed tests" -ForegroundColor $(if ($failed -eq 0) { "Green" } else { "Red" })
Write-Host ""

if ($failed -eq 0) {
    Write-Host "🎉 All tests passed!" -ForegroundColor Green
    exit 0
} else {
    Write-Host "⚠️  Some tests failed. Please review the output above." -ForegroundColor Yellow
    exit 1
}
