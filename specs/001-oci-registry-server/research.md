# Research: OCI Registry with AWS S3 Storage

**Date**: October 27, 2025  
**Feature**: OCI-Compliant Registry Server (dotreg)  
**Purpose**: Research best practices and design decisions for building an OCI registry using .NET and AWS S3

---

## 1. AWS S3 as Primary Storage for OCI Registry

### Decision: Use S3 object keys with hierarchical path structure

**Rationale**:
- S3 provides content-addressed storage naturally suited for immutable blobs and manifests
- S3's built-in integrity checking (ETags) aligns with OCI digest validation requirements
- High durability (99.999999999%) and availability (99.99%) meet registry reliability needs
- S3's native support for byte-range requests enables efficient chunked blob uploads/downloads
- No database overhead - simplifies deployment and reduces operational complexity

**S3 Key Structure**:
```
/repositories/{repository-name}/manifests/{digest}              # Manifests by digest
/repositories/{repository-name}/tags/{tag-name}                 # Tag -> digest mapping (small JSON file)
/repositories/{repository-name}/blobs/{digest}                  # Blobs (layers, configs)
/uploads/{upload-uuid}                                          # Temporary upload sessions
/repositories/{repository-name}/referrers/{subject-digest}      # Referrers index (if enabled)
```

**Alternatives Considered**:
1. **DynamoDB for metadata + S3 for blobs**: Rejected because:
   - Adds operational complexity (two services to manage)
   - DynamoDB adds cost for simple key-value lookups
   - S3 alone provides sufficient query capabilities via LIST operations with prefixes
   
2. **Flat S3 key structure (no slashes)**: Rejected because:
   - Poor S3 console browsability
   - Inefficient prefix-based listing operations
   - Harder to implement tag listing and referrers queries

3. **SQL database (PostgreSQL/MySQL)**: Rejected per user requirements (no SQL database)

**Implementation Notes**:
- Use S3 SDK's `PutObjectAsync` with content-md5 validation for uploads
- Use `GetObjectAsync` with byte-range support for chunked downloads
- Use `ListObjectsV2Async` with prefix for tag listing and referrers queries
- Store upload session state in S3 (upload UUID as key) with lifecycle policy for 24-hour expiration
- Use S3's conditional requests (If-None-Match) for optimistic concurrency where needed

---

## 2. .NET 8.0 and ASP.NET Core Best Practices for OCI Registry

### Decision: Use ASP.NET Core 8.0 with minimal API or controller-based routing

**Rationale**:
- .NET 8.0 is the current LTS release with excellent performance and HTTP/2 support
- ASP.NET Core provides excellent HTTP semantics (headers, status codes, streaming)
- Built-in dependency injection simplifies service architecture
- Native support for OpenAPI/Swagger documentation
- Excellent async/await support for I/O-bound S3 operations

**Architecture Pattern**: Clean Architecture with three layers
- **API Layer** (Dotreg.Api): Controllers, middleware, DTOs, OpenAPI
- **Core Layer** (Dotreg.Core): Business logic, domain models, service interfaces
- **Storage Layer** (Dotreg.Storage.S3): S3-specific implementation

**Key .NET Patterns**:
1. **Use `Stream` for blob transfers**: Avoid loading entire blobs in memory
   ```csharp
   await s3Client.GetObjectAsync(request).CopyToAsync(Response.Body);
   ```

2. **Use `IMemoryCache` for small manifest caching**: Optional performance optimization
   ```csharp
   _cache.GetOrCreateAsync(digest, async () => await FetchManifestAsync(digest));
   ```

3. **Use middleware for cross-cutting concerns**:
   - Error handling (OCI error format)
   - Request logging (structured logs)
   - Validation (repository/tag names)

4. **Use `IOptions<T>` for configuration**:
   ```csharp
   services.Configure<S3Config>(configuration.GetSection("S3"));
   ```

**Alternatives Considered**:
1. **Minimal APIs (Program.cs only)**: Rejected because:
   - OCI API has 20+ endpoints; controllers provide better organization
   - Harder to apply filters/middleware per route group
   
2. **Separate microservices for read/write**: Rejected because:
   - Premature optimization for initial version
   - Added deployment complexity
   - Can refactor later if needed

**Implementation Notes**:
- Use `[ApiController]` attribute for automatic model validation
- Return `FileStreamResult` for blob downloads (enables byte-range support)
- Use `ProblemDetails` as base for OCI error responses
- Configure Kestrel limits (max request body size for manifests)

---

## 3. OCI Distribution Spec v1.1.1 Implementation Requirements

### Decision: Strictly implement all mandatory endpoints; make deletion and referrers optional (configurable)

**Rationale**:
- Spec compliance ensures compatibility with Docker, ORAS, and other clients
- Mandatory endpoints (pull, push, tags) provide core functionality
- Optional features (deletion, referrers) can be enabled via configuration

**Mandatory Endpoints** (MUST implement):
- `GET /v2/` - Version check
- `GET /v2/{name}/manifests/{reference}` - Pull manifest
- `HEAD /v2/{name}/manifests/{reference}` - Check manifest existence
- `PUT /v2/{name}/manifests/{reference}` - Push manifest
- `GET /v2/{name}/blobs/{digest}` - Pull blob
- `HEAD /v2/{name}/blobs/{digest}` - Check blob existence
- `POST /v2/{name}/blobs/uploads/` - Initiate blob upload
- `PATCH /v2/{name}/blobs/uploads/{uuid}` - Upload blob chunk
- `PUT /v2/{name}/blobs/uploads/{uuid}` - Complete blob upload
- `GET /v2/{name}/blobs/uploads/{uuid}` - Get upload progress
- `GET /v2/{name}/tags/list` - List tags

**Optional Endpoints** (configurable):
- `DELETE /v2/{name}/manifests/{reference}` - Delete manifest
- `DELETE /v2/{name}/blobs/{digest}` - Delete blob
- `GET /v2/{name}/referrers/{digest}` - Query referrers
- `POST /v2/{name}/blobs/uploads/?mount={digest}&from={source}` - Mount blob

**Key Spec Requirements**:
1. **Repository name validation**: `[a-z0-9]+((\.|_|__|-+)[a-z0-9]+)*(\/[a-z0-9]+((\.|_|__|-+)[a-z0-9]+)*)*`
2. **Tag name validation**: `[a-zA-Z0-9_][a-zA-Z0-9._-]{0,127}` (max 128 chars)
3. **Digest format**: `{algorithm}:{hex}` (e.g., `sha256:abc123...`)
4. **Error response format**:
   ```json
   {
     "errors": [
       {
         "code": "MANIFEST_UNKNOWN",
         "message": "manifest unknown",
         "detail": "additional context"
       }
     ]
   }
   ```

5. **Required response headers**:
   - `Docker-Content-Digest`: Content digest for manifests/blobs
   - `Content-Type`: Manifest media type or `application/octet-stream` for blobs
   - `Content-Length`: Size of response body
   - `Location`: For upload sessions and successful uploads

**Alternatives Considered**:
1. **Implement only pull (read-only registry)**: Rejected because user story 2 (push) is P2 priority
2. **Skip referrers API entirely**: Rejected because it's in spec v1.1.1 and needed for modern workflows

**Implementation Notes**:
- Create `OciErrorCode` enum with all spec-defined error codes
- Use `ActionFilterAttribute` for repository/tag name validation
- Store manifest Content-Type in S3 metadata for correct retrieval
- Implement upload session expiration using S3 lifecycle policies

---

## 4. Content Addressing and Digest Validation

### Decision: Use SHA-256 for digest calculation; validate on upload completion

**Rationale**:
- SHA-256 is OCI default and universally supported
- Content-addressed storage ensures immutability
- Validation on PUT completion prevents corrupted data

**Digest Calculation Flow**:
1. **Client provides digest**: Client calculates SHA-256 of content before upload
2. **Server validates on completion**: Server recalculates digest from uploaded bytes
3. **Reject on mismatch**: Return HTTP 400 `DIGEST_INVALID` if client digest doesn't match

**Implementation Strategy**:
```csharp
using var sha256 = SHA256.Create();
using var s3Stream = await s3Client.GetObjectStreamAsync(key);
var computedHash = await sha256.ComputeHashAsync(s3Stream);
var computedDigest = $"sha256:{Convert.ToHexString(computedHash).ToLowerInvariant()}";

if (computedDigest != providedDigest)
{
    throw new DigestMismatchException();
}
```

**S3 ETag Limitation**:
- S3 ETag is NOT suitable for OCI digest validation
- ETag for multipart uploads is MD5 of part MD5s (not content MD5)
- Must calculate SHA-256 independently

**Alternatives Considered**:
1. **Trust client digest without validation**: Rejected because spec requires validation
2. **Use S3 Content-MD5**: Rejected because OCI uses SHA-256, not MD5
3. **Calculate digest during upload**: Rejected because chunked uploads require assembly first

**Implementation Notes**:
- Stream large blobs to avoid memory exhaustion
- For chunked uploads, validate digest only on final PUT
- Cache validated digests to avoid recalculation on subsequent pulls

---

## 5. Upload Session Management

### Decision: Store upload session metadata in S3 with lifecycle-based expiration

**Rationale**:
- Upload sessions need to track: UUID, repository, uploaded byte ranges, expiration
- S3 object metadata can store session state (small JSON document)
- S3 lifecycle policies automatically delete expired sessions after 24 hours
- No need for separate database or in-memory state management

**Upload Session State**:
```json
{
  "uuid": "550e8400-e29b-41d4-a716-446655440000",
  "repository": "myorg/myapp",
  "digest": "sha256:abc123...",
  "uploadedRanges": [
    {"start": 0, "end": 1048575},
    {"start": 1048576, "end": 2097151}
  ],
  "totalSize": 10485760,
  "createdAt": "2025-10-27T10:00:00Z"
}
```

**S3 Storage**:
- Key: `/uploads/{uuid}/metadata.json`
- Blob chunks: `/uploads/{uuid}/chunks/{chunk-number}`
- On completion: Assemble chunks into final blob at `/repositories/{name}/blobs/{digest}`

**Lifecycle Policy**:
```json
{
  "Rules": [{
    "Id": "ExpireUploadSessions",
    "Prefix": "uploads/",
    "Status": "Enabled",
    "Expiration": {"Days": 1}
  }]
}
```

**Alternatives Considered**:
1. **In-memory session storage**: Rejected because:
   - Lost on server restart
   - Doesn't scale across multiple instances
   - No built-in expiration mechanism

2. **Redis for session state**: Rejected because:
   - Adds external dependency (violates S3-only requirement)
   - S3 is sufficient for this use case

3. **Single large S3 object with byte-range PUTs**: Rejected because:
   - S3 doesn't support byte-range PUT (only byte-range GET)
   - Must use multipart upload API

**Implementation Notes**:
- Use S3 multipart upload API for chunked uploads
- Store UploadId from S3 in session metadata
- On completion, call CompleteMultipartUpload and move to final location
- Handle resume by returning uploaded byte ranges from S3 metadata

---

## 6. Tag Management and Listing

### Decision: Store tags as individual S3 objects containing digest pointers

**Rationale**:
- Tags are mutable pointers to immutable manifests
- Each tag is small (just digest + timestamp)
- S3 LIST operation with prefix enables efficient tag listing

**Tag Storage**:
- Key: `/repositories/{name}/tags/{tag-name}`
- Content:
  ```json
  {
    "digest": "sha256:abc123...",
    "updatedAt": "2025-10-27T10:00:00Z"
  }
  ```

**Tag Listing Implementation**:
```csharp
var listRequest = new ListObjectsV2Request
{
    BucketName = bucketName,
    Prefix = $"repositories/{name}/tags/",
    MaxKeys = pageSize
};

var response = await s3Client.ListObjectsV2Async(listRequest);
var tags = response.S3Objects
    .Select(obj => obj.Key.Split('/').Last())
    .OrderBy(tag => tag, StringComparer.Ordinal)
    .ToList();
```

**Pagination**:
- Use `MaxKeys` (n parameter) to limit results
- Use `StartAfter` (last parameter) for pagination
- Return `Link` header with next page URL if more results available

**Alternatives Considered**:
1. **Store all tags in single index file**: Rejected because:
   - Concurrent tag updates require locking or optimistic concurrency
   - File grows unbounded for repositories with many tags
   - S3 eventual consistency issues with high update rates

2. **DynamoDB for tag index**: Rejected per no-database requirement

**Implementation Notes**:
- Last-write-wins for concurrent tag updates (no conflict detection)
- Use S3 versioning (optional) for tag history
- Consider caching frequently accessed tags (e.g., "latest")

---

## 7. Referrers API Implementation

### Decision: Use image index manifest stored at referrers endpoint; fallback to tag schema

**Rationale**:
- Referrers API (OCI spec v1.1+) provides structured way to link artifacts
- Clients query `/v2/{name}/referrers/{digest}` to find related artifacts
- Fallback to tag schema (`sha256-{digest}`) for backward compatibility

**Referrers Storage**:
- Key: `/repositories/{name}/referrers/{subject-digest}/index.json`
- Content: Image index manifest listing all referrers
  ```json
  {
    "schemaVersion": 2,
    "mediaType": "application/vnd.oci.image.index.v1+json",
    "manifests": [
      {
        "mediaType": "application/vnd.oci.image.manifest.v1+json",
        "size": 1234,
        "digest": "sha256:signature123...",
        "artifactType": "application/vnd.dev.cosign.simplesigning.v1+json"
      }
    ]
  }
  ```

**Update Flow**:
1. Client pushes manifest with `subject` field
2. Server extracts subject digest from manifest
3. Server reads existing referrers index (if exists)
4. Server adds new referrer to index
5. Server writes updated index back to S3

**Filtering**:
- Support `artifactType` query parameter
- Filter manifests array before returning
- Include `OCI-Filters-Applied: artifactType` header

**Alternatives Considered**:
1. **Only support tag schema (no API endpoint)**: Rejected because:
   - Spec v1.1.1 includes referrers API
   - Modern clients expect API endpoint

2. **Separate S3 object per referrer**: Rejected because:
   - Requires multiple S3 requests to build complete list
   - Harder to implement filtering

**Implementation Notes**:
- Use optimistic concurrency (ETag-based) for index updates
- Retry with exponential backoff on concurrent update conflicts
- Consider eventual consistency window (read-after-write)
- Make referrers API optional (configuration flag)

---

## 8. Testing Strategy with LocalStack

### Decision: Use Testcontainers with LocalStack for S3 integration tests

**Rationale**:
- LocalStack provides S3-compatible API for local testing
- Testcontainers manages LocalStack lifecycle in tests
- Enables integration tests without AWS account/costs
- Tests run in CI/CD without external dependencies

**Test Layers**:
1. **Unit Tests** (Dotreg.Core.Tests):
   - Business logic with mocked storage
   - Validation rules
   - Digest calculation

2. **Integration Tests** (Dotreg.Storage.S3.Tests):
   - S3 operations with LocalStack
   - Upload/download flows
   - Lifecycle policies (manual verification)

3. **API Tests** (Dotreg.Api.Tests):
   - HTTP endpoint tests with TestServer
   - Mock or LocalStack S3 backend
   - OCI protocol compliance

4. **E2E Tests** (Dotreg.Integration.Tests):
   - Docker CLI push/pull
   - ORAS CLI operations
   - Real client compatibility

**LocalStack Setup**:
```csharp
var localstack = new ContainerBuilder()
    .WithImage("localstack/localstack:latest")
    .WithPortBinding(4566, 4566)
    .WithEnvironment("SERVICES", "s3")
    .Build();

await localstack.StartAsync();

var s3Client = new AmazonS3Client(
    new BasicAWSCredentials("test", "test"),
    new AmazonS3Config
    {
        ServiceURL = "http://localhost:4566",
        ForcePathStyle = true
    }
);
```

**Alternatives Considered**:
1. **Only unit tests with mocks**: Rejected because:
   - Doesn't validate S3 API integration
   - Miss S3-specific behaviors (eventual consistency, ETags)

2. **Test against real AWS S3**: Rejected because:
   - Requires AWS credentials
   - Costs money
   - Slower than local tests
   - Can't run in CI without credentials

**Implementation Notes**:
- Use `IAsyncLifetime` for test class setup/teardown
- Create isolated S3 buckets per test class
- Clean up test data after each test
- Use realistic blob sizes (avoid 1GB files in tests)

---

## 9. Performance Optimization Strategies

### Decision: Stream large objects, cache small manifests, implement concurrent upload support

**Rationale**:
- Registry performance directly impacts user experience (pull/push times)
- Streaming prevents memory exhaustion
- Caching reduces S3 API calls for frequently accessed content
- Concurrent operations improve throughput

**Optimization Techniques**:

1. **Streaming for blobs**:
   ```csharp
   // Don't do this (loads entire blob in memory)
   var bytes = await s3Client.GetObjectAsync(...).ReadAllBytesAsync();
   
   // Do this (streams directly to response)
   await s3Client.GetObjectAsync(...).CopyToAsync(Response.Body);
   ```

2. **Manifest caching** (optional):
   - Cache small manifests (<1MB) in memory
   - Use LRU eviction policy
   - TTL: 5-10 minutes
   - Invalidate on manifest PUT

3. **Concurrent blob uploads**:
   - S3 multipart upload supports parallel part uploads
   - Client can upload chunks in parallel
   - Server assembles on completion

4. **Connection pooling**:
   - Configure HttpClient for S3 SDK (reuse connections)
   - Set MaxConnectionsPerServer appropriately

5. **Compression** (optional):
   - S3 supports gzip content encoding
   - Consider for manifest responses
   - Don't compress blobs (already compressed layers)

**Performance Targets**:
- Manifest GET: <200ms p95
- Blob GET (streaming): throughput limited by network/S3, not server
- Manifest PUT: <500ms p95
- Tag listing (1000 tags): <2s p95

**Alternatives Considered**:
1. **CDN in front of registry**: Out of scope for initial version, but good future enhancement
2. **Blob deduplication**: S3 handles via digest-based keys automatically
3. **Distributed cache (Redis)**: Rejected per S3-only requirement

**Implementation Notes**:
- Use `IMemoryCache` (ASP.NET Core built-in)
- Monitor cache hit/miss rates with metrics
- Ensure caching doesn't violate consistency requirements
- Load test with realistic blob sizes (100MB-1GB layers)

---

## 10. Configuration and Deployment

### Decision: Environment-based configuration with Kubernetes deployment target

**Rationale**:
- 12-factor app principles: config in environment
- Kubernetes is common deployment target for registries
- Docker Compose for local development

**Configuration Structure**:
```json
{
  "S3": {
    "BucketName": "dotreg-storage",
    "Region": "us-east-1",
    "AccessKeyId": "",  // Empty = use IAM role
    "SecretAccessKey": "",
    "ServiceUrl": ""  // For LocalStack
  },
  "Registry": {
    "EnableDeletion": false,
    "EnableReferrersApi": true,
    "MaxManifestSize": 4194304,
    "UploadSessionExpiration": "24:00:00",
    "EnableAuth": false
  },
  "Logging": {
    "Level": "Information",
    "Format": "Json"
  }
}
```

**Environment Variables** (override appsettings.json):
- `S3__BucketName`
- `S3__Region`
- `Registry__EnableDeletion`

**Kubernetes Deployment**:
- Use Deployment with multiple replicas for HA
- Use Service (ClusterIP) + Ingress with TLS
- Store S3 credentials in Kubernetes Secret
- Use IAM roles for service accounts (IRSA) for credential-less access

**Docker Compose** (local dev):
```yaml
services:
  dotreg:
    build: .
    ports:
      - "5000:8080"
    environment:
      - S3__ServiceUrl=http://localstack:4566
      - S3__BucketName=dotreg-dev
    depends_on:
      - localstack
  
  localstack:
    image: localstack/localstack
    ports:
      - "4566:4566"
    environment:
      - SERVICES=s3
```

**Alternatives Considered**:
1. **Configuration in S3**: Rejected because requires bootstrap config to access S3
2. **Consul/etcd for config**: Rejected as overkill for initial version

**Implementation Notes**:
- Use `IConfiguration` and `IOptions<T>` pattern
- Validate configuration on startup (fail fast)
- Document all configuration options in README
- Provide example configurations for Docker and Kubernetes

---

## Summary

All technical decisions have been made for implementing dotreg:

✅ **Storage**: AWS S3 with hierarchical key structure  
✅ **Framework**: .NET 9.0 with ASP.NET Core  
✅ **Architecture**: Three-layer clean architecture  
✅ **OCI Compliance**: Full v1.1.1 spec implementation  
✅ **Content Addressing**: SHA-256 with server-side validation  
✅ **Upload Sessions**: S3-based with lifecycle expiration  
✅ **Tag Management**: Individual S3 objects with LIST-based queries  
✅ **Referrers API**: Image index with artifactType filtering  
✅ **Testing**: Testcontainers + LocalStack for integration tests  
✅ **Performance**: Streaming, caching, concurrent uploads  
✅ **Deployment**: Kubernetes-ready with environment-based config  

No outstanding "NEEDS CLARIFICATION" items remain. Ready to proceed to Phase 1: Design & Contracts.
