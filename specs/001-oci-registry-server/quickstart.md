# Quickstart Guide: dotreg Development

**Date**: October 27, 2025  
**Feature**: OCI-Compliant Registry Server  
**Target Audience**: Developers working on dotreg

---

## Prerequisites

Before you begin, ensure you have the following installed:

- **.NET 8.0 SDK** (LTS)
  - Download: https://dotnet.microsoft.com/download/dotnet/8.0
  - Verify: `dotnet --version` (should show 8.0.x)

- **Docker Desktop** (for testing with Docker CLI)
  - Download: https://www.docker.com/products/docker-desktop
  - Verify: `docker --version`

- **AWS CLI** (for S3 setup, optional for local development)
  - Download: https://aws.amazon.com/cli/
  - Verify: `aws --version`

- **Git** (for version control)
  - Verify: `git --version`

- **IDE**: Visual Studio 2022, VS Code with C# extension, or JetBrains Rider

---

## Project Structure Overview

```
dotreg/
├── src/
│   ├── Dotreg.Api/              # ASP.NET Core Web API
│   ├── Dotreg.Core/             # Business logic
│   └── Dotreg.Storage.S3/       # S3 storage implementation
├── tests/
│   ├── Dotreg.Api.Tests/        # API integration tests
│   ├── Dotreg.Core.Tests/       # Unit tests
│   ├── Dotreg.Storage.S3.Tests/ # S3 storage tests
│   └── Dotreg.Integration.Tests/# E2E tests with Docker
├── specs/
│   └── 001-oci-registry-server/ # Feature specification and design
├── dotreg.sln                   # Solution file
└── README.md
```

---

## Local Development Setup

### 1. Clone the Repository

```bash
git clone https://github.com/shizhMSFT/dotreg.git
cd dotreg
git checkout 001-oci-registry-server
```

### 2. Start LocalStack (S3 Emulator)

LocalStack provides a local S3-compatible service for development without AWS account:

**Using Docker Compose**:

Create `docker-compose.dev.yml`:
```yaml
version: '3.8'

services:
  localstack:
    image: localstack/localstack:latest
    ports:
      - "4566:4566"
    environment:
      - SERVICES=s3
      - DEBUG=1
      - DATA_DIR=/tmp/localstack/data
    volumes:
      - localstack-data:/tmp/localstack
    healthcheck:
      test: ["CMD", "curl", "-f", "http://localhost:4566/_localstack/health"]
      interval: 10s
      timeout: 5s
      retries: 5

volumes:
  localstack-data:
```

**Start LocalStack**:
```bash
docker-compose -f docker-compose.dev.yml up -d
```

**Verify LocalStack is running**:
```bash
curl http://localhost:4566/_localstack/health
```

### 3. Create S3 Bucket in LocalStack

```bash
# Configure AWS CLI for LocalStack
export AWS_ACCESS_KEY_ID=test
export AWS_SECRET_ACCESS_KEY=test
export AWS_DEFAULT_REGION=us-east-1

# Create bucket
aws --endpoint-url=http://localhost:4566 s3 mb s3://dotreg-dev

# Verify bucket creation
aws --endpoint-url=http://localhost:4566 s3 ls
```

### 4. Configure Application Settings

Create or update `src/Dotreg.Api/appsettings.Development.json`:

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning",
      "Dotreg": "Debug"
    }
  },
  "S3": {
    "BucketName": "dotreg-dev",
    "Region": "us-east-1",
    "ServiceUrl": "http://localhost:4566",
    "AccessKeyId": "test",
    "SecretAccessKey": "test",
    "ForcePathStyle": true
  },
  "Registry": {
    "EnableDeletion": true,
    "EnableReferrersApi": true,
    "MaxManifestSize": 4194304,
    "UploadSessionExpiration": "24:00:00",
    "EnableAuth": false
  }
}
```

### 5. Build the Solution

```bash
# Restore dependencies and build
dotnet restore
dotnet build

# Or build specific project
dotnet build src/Dotreg.Api/Dotreg.Api.csproj
```

### 6. Run the Registry

**Option A: Using dotnet run**:
```bash
cd src/Dotreg.Api
dotnet run
```

**Option B: Using Visual Studio**:
- Open `dotreg.sln`
- Set `Dotreg.Api` as startup project
- Press F5 to run with debugging

**Option C: Using VS Code**:
- Open folder in VS Code
- Select "Run and Debug" → ".NET Core Launch (web)"
- Press F5

The registry will start at `http://localhost:5000` (or `https://localhost:5001` for HTTPS).

### 7. Verify Registry is Running

```bash
# Check API version endpoint
curl http://localhost:5000/v2/

# Expected response: {}
# Expected header: Docker-Distribution-Api-Version: registry/2.0
```

---

## Running Tests

### Unit Tests

Run all unit tests:
```bash
dotnet test --filter FullyQualifiedName~Dotreg.Core.Tests
```

Run specific test class:
```bash
dotnet test --filter FullyQualifiedName~Dotreg.Core.Tests.Services.RegistryServiceTests
```

### Integration Tests (with LocalStack)

Ensure LocalStack is running, then:
```bash
dotnet test --filter FullyQualifiedName~Dotreg.Storage.S3.Tests
```

### API Tests

```bash
dotnet test --filter FullyQualifiedName~Dotreg.Api.Tests
```

### E2E Tests (with Docker Client)

```bash
# Ensure registry is running at localhost:5000
dotnet test --filter FullyQualifiedName~Dotreg.Integration.Tests
```

### Run All Tests

```bash
dotnet test
```

### Test Coverage

Generate coverage report (requires coverlet):
```bash
dotnet test /p:CollectCoverage=true /p:CoverletOutputFormat=opencover
```

---

## Testing with Docker CLI

### 1. Configure Docker to Use Insecure Registry

**For localhost testing**, Docker needs to allow insecure HTTP registry:

**Linux/Mac** (`/etc/docker/daemon.json`):
```json
{
  "insecure-registries": ["localhost:5000"]
}
```

**Windows** (Docker Desktop Settings → Docker Engine):
```json
{
  "insecure-registries": ["localhost:5000"]
}
```

Restart Docker Desktop after changes.

### 2. Push an Image

```bash
# Pull a small test image from Docker Hub
docker pull busybox:latest

# Tag it for your local registry
docker tag busybox:latest localhost:5000/myorg/busybox:test

# Push to dotreg
docker push localhost:5000/myorg/busybox:test
```

**Expected output**:
```
The push refers to repository [localhost:5000/myorg/busybox]
<layer digests>
test: digest: sha256:abc123... size: 527
```

### 3. Pull the Image

```bash
# Remove local image
docker rmi localhost:5000/myorg/busybox:test
docker rmi busybox:latest

# Pull from dotreg
docker pull localhost:5000/myorg/busybox:test
```

### 4. Verify in S3 (LocalStack)

```bash
# List objects in bucket
aws --endpoint-url=http://localhost:4566 s3 ls s3://dotreg-dev/repositories/ --recursive

# You should see:
# repositories/myorg/busybox/manifests/sha256:...
# repositories/myorg/busybox/blobs/sha256:...
# repositories/myorg/busybox/tags/test
```

---

## Testing with ORAS CLI

ORAS (OCI Registry As Storage) is a tool for pushing/pulling arbitrary artifacts.

### 1. Install ORAS

```bash
# macOS
brew install oras

# Linux (download from GitHub releases)
curl -LO https://github.com/oras-project/oras/releases/download/v1.1.0/oras_1.1.0_linux_amd64.tar.gz
tar -xvf oras_1.1.0_linux_amd64.tar.gz
sudo mv oras /usr/local/bin/

# Windows (using Scoop)
scoop install oras
```

### 2. Push an Artifact

```bash
# Create a test file
echo "Hello from dotreg!" > hello.txt

# Push to registry
oras push localhost:5000/myorg/artifacts:hello \
  hello.txt:text/plain

# Expected output:
# Uploading abc123... hello.txt
# Uploaded  abc123... hello.txt
# Pushed localhost:5000/myorg/artifacts:hello
# Digest: sha256:def456...
```

### 3. Pull the Artifact

```bash
# Pull to current directory
oras pull localhost:5000/myorg/artifacts:hello

# Verify file contents
cat hello.txt
```

---

## Development Workflow

### 1. Create a Feature Branch

```bash
git checkout -b feature/my-feature
```

### 2. Write Tests First (TDD)

For a new feature, start by writing failing tests:

```csharp
// tests/Dotreg.Core.Tests/Services/RegistryServiceTests.cs
[Fact]
public async Task GetManifest_WhenManifestExists_ReturnsManifest()
{
    // Arrange
    var digest = "sha256:abc123...";
    var expectedManifest = CreateTestManifest();
    _mockStorage.Setup(s => s.GetManifestAsync(It.IsAny<string>(), digest))
        .ReturnsAsync(expectedManifest);

    // Act
    var result = await _registryService.GetManifestAsync("myorg/myapp", digest);

    // Assert
    result.Should().NotBeNull();
    result.Digest.Should().Be(digest);
}
```

Run test (should fail):
```bash
dotnet test --filter GetManifest_WhenManifestExists_ReturnsManifest
```

### 3. Implement the Feature

```csharp
// src/Dotreg.Core/Services/RegistryService.cs
public async Task<Manifest> GetManifestAsync(string repositoryName, string digest)
{
    // Implementation here
}
```

### 4. Run Tests (should pass now)

```bash
dotnet test
```

### 5. Refactor and Clean Up

- Remove code duplication
- Add XML documentation comments
- Ensure naming conventions are followed

### 6. Commit Changes

```bash
git add .
git commit -m "feat: implement manifest retrieval"
```

### 7. Push and Create Pull Request

```bash
git push origin feature/my-feature
```

---

## Common Development Tasks

### View Logs

**Structured JSON logs** are written to console in Development mode:

```bash
# Run with detailed logs
dotnet run --project src/Dotreg.Api/Dotreg.Api.csproj -- --environment Development
```

### Debug S3 Operations

Enable S3 SDK logging in `appsettings.Development.json`:

```json
{
  "Logging": {
    "LogLevel": {
      "AWSSDK": "Debug"
    }
  }
}
```

### Clear LocalStack Data

```bash
# Stop and remove containers
docker-compose -f docker-compose.dev.yml down -v

# Restart fresh
docker-compose -f docker-compose.dev.yml up -d

# Recreate bucket
aws --endpoint-url=http://localhost:4566 s3 mb s3://dotreg-dev
```

### Hot Reload

.NET 8.0 supports hot reload for quick iteration:

```bash
dotnet watch --project src/Dotreg.Api/Dotreg.Api.csproj
```

Make changes to code → Save → Application reloads automatically.

---

## Troubleshooting

### Issue: "Connection refused" to LocalStack

**Solution**:
- Verify LocalStack is running: `docker ps`
- Check health: `curl http://localhost:4566/_localstack/health`
- Restart LocalStack: `docker-compose -f docker-compose.dev.yml restart`

### Issue: Docker push fails with "server gave HTTP response to HTTPS client"

**Solution**:
- Ensure `localhost:5000` is in `insecure-registries` in Docker daemon config
- Restart Docker Desktop

### Issue: Tests fail with S3 errors

**Solution**:
- Ensure LocalStack is running before tests
- Check test uses correct endpoint URL (`http://localhost:4566`)
- Verify bucket exists: `aws --endpoint-url=http://localhost:4566 s3 ls`

### Issue: High memory usage during blob uploads

**Solution**:
- Ensure streaming is used (not loading entire blob in memory)
- Check that `Stream.CopyToAsync()` is used for blob transfers
- Monitor with: `dotnet-counters monitor --process-id <PID>`

---

## Next Steps

1. **Read the Spec**: Review `specs/001-oci-registry-server/spec.md` for requirements
2. **Review Architecture**: See `specs/001-oci-registry-server/data-model.md` for entity design
3. **Check API Contract**: Explore `specs/001-oci-registry-server/contracts/openapi.yaml`
4. **Run Tests**: Ensure all tests pass before making changes
5. **Follow TDD**: Write tests first, then implement features

---

## Useful Commands Reference

```bash
# Build
dotnet build

# Run
dotnet run --project src/Dotreg.Api/Dotreg.Api.csproj

# Test
dotnet test
dotnet test --filter <TestName>
dotnet test /p:CollectCoverage=true

# Watch (hot reload)
dotnet watch --project src/Dotreg.Api/Dotreg.Api.csproj

# Format code
dotnet format

# LocalStack
docker-compose -f docker-compose.dev.yml up -d
docker-compose -f docker-compose.dev.yml down

# AWS CLI (LocalStack)
aws --endpoint-url=http://localhost:4566 s3 ls
aws --endpoint-url=http://localhost:4566 s3 ls s3://dotreg-dev --recursive

# Docker
docker tag <image> localhost:5000/<repo>:<tag>
docker push localhost:5000/<repo>:<tag>
docker pull localhost:5000/<repo>:<tag>

# ORAS
oras push localhost:5000/<repo>:<tag> <file>:<mediaType>
oras pull localhost:5000/<repo>:<tag>
```

---

## Resources

- **OCI Distribution Spec**: https://github.com/opencontainers/distribution-spec/blob/v1.1.1/spec.md
- **OCI Image Spec**: https://github.com/opencontainers/image-spec/blob/v1.1.1/spec.md
- **.NET 8.0 Docs**: https://learn.microsoft.com/en-us/dotnet/core/whats-new/dotnet-8
- **ASP.NET Core**: https://learn.microsoft.com/en-us/aspnet/core
- **AWS S3 SDK for .NET**: https://docs.aws.amazon.com/sdk-for-net/v3/developer-guide/s3-apis-intro.html
- **LocalStack Docs**: https://docs.localstack.cloud/
- **Docker Registry API**: https://docs.docker.com/registry/spec/api/
- **ORAS**: https://oras.land/

---

**Happy coding! 🚀**
