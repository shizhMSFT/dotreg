# Quick Start Guide - dotreg

## Prerequisites

- .NET 8.0 SDK
- AWS S3 bucket (or LocalStack for local development)
- Docker (optional, for testing)

## Configuration

Edit `src/Dotreg.Api/appsettings.json`:

```jsonc
{
  "S3": {
    "BucketName": "your-bucket-name",
    "Region": "us-east-1",
    "ServiceUrl": null,  // Set for LocalStack: "http://localhost:4566"
    "AccessKeyId": null,  // Or use AWS credentials
    "SecretAccessKey": null
  },
  "Registry": {
    "EnableDeletion": false,  // Set true to enable DELETE operations
    "EnableReferrersApi": true,
    "MaxManifestSizeBytes": 4194304
  }
}
```

## Run Locally

```bash
# Build the solution
dotnet build

# Run tests
dotnet test

# Run the API
cd src/Dotreg.Api
dotnet run

# Registry will be available at http://localhost:5000
```

## Test with Docker

```bash
# Check API version
curl http://localhost:5000/v2/

# Expected: 200 OK with {"version":"registry/2.0"}

# Tag and push a local image
docker tag alpine:latest localhost:5000/myapp/alpine:latest
docker push localhost:5000/myapp/alpine:latest

# Pull the image
docker pull localhost:5000/myapp/alpine:latest

# List tags
curl http://localhost:5000/v2/myapp/alpine/tags/list

# Expected: {"name":"myapp/alpine","tags":["latest"]}
```

## LocalStack Setup (Development)

```bash
# Start LocalStack with S3
docker run -d -p 4566:4566 localstack/localstack

# Create bucket
aws --endpoint-url=http://localhost:4566 s3 mb s3://dotreg

# Update appsettings.Development.json
{
  "S3": {
    "ServiceUrl": "http://localhost:4566",
    "BucketName": "dotreg",
    "AccessKeyId": "test",
    "SecretAccessKey": "test"
  }
}
```

## Production Deployment

### Docker

```bash
# Build Docker image
docker build -t dotreg:latest .

# Run with environment variables
docker run -p 5000:8080 \
  -e S3__BucketName=your-bucket \
  -e S3__Region=us-east-1 \
  -e Registry__EnableDeletion=false \
  dotreg:latest
```

### Kubernetes

See `k8s/` directory for Kubernetes manifests (coming soon).

## API Endpoints

### Registry Operations
- `GET /v2/` - API version check
- `GET /v2/{name}/manifests/{reference}` - Get manifest
- `HEAD /v2/{name}/manifests/{reference}` - Check manifest
- `PUT /v2/{name}/manifests/{reference}` - Upload manifest
- `DELETE /v2/{name}/manifests/{reference}` - Delete manifest*
- `GET /v2/{name}/blobs/{digest}` - Get blob
- `HEAD /v2/{name}/blobs/{digest}` - Check blob
- `DELETE /v2/{name}/blobs/{digest}` - Delete blob*
- `GET /v2/{name}/tags/list` - List tags

### Upload Operations
- `POST /v2/{name}/blobs/uploads/` - Start upload
- `PATCH /v2/{name}/blobs/uploads/{uuid}` - Upload chunk
- `PUT /v2/{name}/blobs/uploads/{uuid}?digest=` - Complete upload
- `GET /v2/{name}/blobs/uploads/{uuid}` - Get progress
- `DELETE /v2/{name}/blobs/uploads/{uuid}` - Cancel upload

\* Requires `Registry:EnableDeletion=true`

## Troubleshooting

### S3 Permissions Required
```json
{
  "Version": "2012-10-17",
  "Statement": [
    {
      "Effect": "Allow",
      "Action": [
        "s3:GetObject",
        "s3:PutObject",
        "s3:DeleteObject",
        "s3:ListBucket"
      ],
      "Resource": [
        "arn:aws:s3:::your-bucket/*",
        "arn:aws:s3:::your-bucket"
      ]
    }
  ]
}
```

### Common Issues

**Docker push fails with 500 error**
- Check S3 bucket exists and credentials are valid
- Check logs: `dotnet run --urls http://localhost:5000`

**Manifest upload fails with SIZE_INVALID**
- Increase `Registry:MaxManifestSizeBytes` (default 4MB)

**Delete returns 405**
- Set `Registry:EnableDeletion=true` in configuration

## Development

```bash
# Run with hot reload
dotnet watch run --project src/Dotreg.Api

# Run specific test class
dotnet test --filter FullyQualifiedName~ManifestEndpointTests

# Run with verbose logging
dotnet run --project src/Dotreg.Api -- --environment Development
```

## Security Considerations

1. **Authentication**: Currently no authentication - add auth middleware in production
2. **HTTPS**: Use reverse proxy (nginx/traefik) for TLS termination
3. **Deletion**: Keep disabled unless needed (enables accidental data loss)
4. **S3 Access**: Use IAM roles instead of access keys when possible
5. **Network**: Restrict access to trusted networks

## Monitoring

- Logs: Structured JSON logging to stdout
- Health: `GET /health` (basic endpoint)
- Metrics: Not yet implemented (see Phase 9 tasks)

---
*Last Updated: October 27, 2025*
