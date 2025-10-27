# dotreg Development Guidelines

Auto-generated from all feature plans. Last updated: 2025-10-27

## Active Technologies

- C# with .NET 8.0 (LTS) (001-oci-registry-server)

## Project Structure

```text
src/
  Dotreg.Api/          # ASP.NET Core Web API
  Dotreg.Core/         # Core business logic
tests/
scripts/
  start-localstack.ps1 # Start LocalStack S3 for development
  run-oras-tests.ps1   # Run ORAS CLI E2E tests
  run-e2e-tests.ps1    # Complete E2E test workflow
  generate-docs.ps1    # Generate test documentation
```

## Commands

### Development
```powershell
# Start LocalStack S3 (required for development)
pwsh scripts/start-localstack.ps1

# Run the registry (from src/Dotreg.Api)
dotnet run

# Build the solution
dotnet build
```

### Testing
```powershell
# Run complete E2E tests (starts LocalStack, registry, runs ORAS tests, cleanup)
pwsh scripts/run-e2e-tests.ps1

# Run ORAS CLI tests only (requires registry running)
pwsh scripts/run-oras-tests.ps1

# Generate test documentation
pwsh scripts/generate-docs.ps1
```

### ORAS CLI Testing
The registry is validated against ORAS CLI 1.3.0 with 13 comprehensive test scenarios:
- Push/pull artifacts with `--plain-http` flag
- Tag listing and manifest operations
- Blob deduplication across versions
- Multiple repository support
- Referrer attachments (signatures, SBOMs, attestations)
- Referrers API with artifact type filtering
- Manifest deletion

See `ORAS_TEST_RESULTS.md` for detailed test results and OCI compliance status.

## Code Style

C# with .NET 8.0 (LTS): Follow standard conventions

## Recent Changes

- 001-oci-registry-server: Added C# with .NET 8.0 (LTS)

<!-- MANUAL ADDITIONS START -->
<!-- MANUAL ADDITIONS END -->
