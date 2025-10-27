# Implementation Plan: OCI-Compliant Registry Server (dotreg)

**Branch**: `001-oci-registry-server` | **Date**: October 27, 2025 | **Spec**: [spec.md](./spec.md)
**Input**: Feature specification from `/specs/001-oci-registry-server/spec.md`

**Note**: This template is filled in by the `/speckit.plan` command. See `.specify/templates/commands/plan.md` for the execution workflow.

## Summary

Build a .NET-based OCI-compliant container registry server (dotreg) that strictly implements the OCI Distribution Spec v1.1.1 for container image storage and distribution. The registry will use AWS S3 as the exclusive storage backend for manifests and blobs, with no SQL or NoSQL database required. The system will support all standard registry operations: pull/push images, content discovery via tag listing, blob mounting for efficient uploads, referrers API for artifact relationships, and optional deletion capabilities.

## Technical Context

**Language/Version**: C# with .NET 8.0 (LTS)
**Primary Dependencies**: 
- ASP.NET Core (web framework)
- AWSSDK.S3 (AWS S3 SDK for .NET)
- System.Text.Json (JSON serialization)
- SHA256 cryptographic functions (System.Security.Cryptography)

**Storage**: AWS S3 (object storage only - no SQL/NoSQL database)
**Testing**: xUnit with FluentAssertions, Testcontainers for integration tests with LocalStack (S3 emulator)
**Target Platform**: Linux containers (Docker/Kubernetes deployment)
**Project Type**: Web API server (RESTful HTTP service)
**Performance Goals**: 
- 100 concurrent pulls without degradation
- 20 concurrent pushes without corruption
- <2s response for manifest requests
- <2s response for tag listings up to 1000 tags

**Constraints**:
- <200ms p95 latency for API endpoints
- Manifest size limit: minimum 4MB support
- Upload session expiration: 24 hours
- Memory efficient for large blob streaming (chunked transfers)

**Scale/Scope**:
- Support 1000+ repositories
- Handle images with 100+ layers
- Support 100+ referrers per artifact
- Concurrent user operations (100 pulls, 20 pushes)

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

**Phase 0 Status**: ✅ PASSED (research.md completed)  
**Phase 1 Status**: ✅ PASSED (data-model.md, contracts/, quickstart.md completed)

### ✅ I. API-First Design
- **Contract Definition**: ✅ OpenAPI schema defined in `contracts/openapi.yaml` for all OCI Distribution Spec endpoints
- **Versioning**: ✅ API follows `/v2/` path prefix per OCI spec (v1.1.1 compatibility)
- **RESTful Conventions**: ✅ Endpoints follow OCI-specified HTTP semantics (GET for pull, POST/PUT for push, DELETE for removal)
- **Response Standards**: ✅ All responses include proper HTTP status codes, OCI-compliant error JSON format with correlation via Docker-Content-Digest headers
- **Input Validation**: ✅ Repository names, tag names, and digests validated per OCI regex patterns at API boundary
- **Documentation**: ✅ OpenAPI documentation with OCI Distribution Spec v1.1.1 mappings and error code catalog

### ✅ II. Test-Driven Development (NON-NEGOTIABLE)
- **Write Tests First**: ✅ Each user story's acceptance scenarios converted to failing xUnit tests before implementation
- **User Approval**: ✅ Acceptance scenarios are explicitly defined in spec.md and validated
- **Red-Green-Refactor**: ✅ TDD cycle mandatory for all 6 user stories
- **Independent Testability**: ✅ Each story (Pull, Push, Discovery, Lifecycle, Referrers, Mounting) is independently testable
- **Coverage Gates**: ✅ Target 80%+ unit test coverage; integration tests for all API workflows; contract tests validate OCI conformance
- **Test Pyramid**: ✅ Unit tests for business logic, integration tests with LocalStack S3, minimal E2E tests with Docker client

### ✅ III. Observability & Monitoring
- **Structured Logging**: ✅ JSON logging with timestamp, level, trace_id, message, context (repository, digest, operation)
- **Log Levels**: ✅ ERROR (upload failures, corruption), WARN (near limits), INFO (push/pull operations), DEBUG (S3 interactions)
- **Metrics**: ✅ Prometheus metrics - request rate by endpoint, latency p50/p95/p99, error rate by error code, S3 operation latency, blob storage size
- **Health Checks**: ✅ `/health` (liveness) and `/ready` (readiness with S3 connectivity check) endpoints <1s response
- **Distributed Tracing**: ✅ Propagate trace_id via Docker-Upload-UUID for upload sessions
- **Error Tracking**: ✅ Log all 4xx/5xx responses with full context (repository, reference, digest, operation)
- **Audit Logging**: ✅ Log all manifest/blob writes and deletes with timestamp and source IP

### ✅ IV. Performance & Scalability
- **Response Time Budgets**: ✅ <200ms p95 for manifest/blob HEAD/GET requests; chunked uploads for large blobs
- **Concurrency**: ✅ Stateless design enables horizontal scaling; S3 handles concurrent blob access natively
- **Database Optimization**: ✅ N/A (no database - S3 only)
- **Caching Strategy**: ✅ Optional in-memory cache for frequently accessed small manifests (TTL-based); S3 serves as source of truth
- **Rate Limiting**: ✅ Implement per-IP rate limiting for upload endpoints to prevent abuse
- **Load Testing**: ✅ Load tests simulate 100 concurrent pulls and 20 concurrent pushes
- **Resource Limits**: ✅ Configure memory limits for container deployment; use streaming for large blob transfers

### ✅ V. Security by Default
- **Authentication**: ✅ Optional authentication support (can be added via middleware); initial version may allow anonymous access with configuration flag
- **Authorization**: ✅ Repository-level access control can be added via extensible authorization layer
- **Input Sanitization**: ✅ Validate all repository names, tags, digests against OCI regex; prevent path traversal in S3 keys
- **Secrets Management**: ✅ S3 credentials from environment variables; support AWS IAM roles for credential-less access
- **HTTPS Only**: ✅ TLS termination at load balancer/reverse proxy; application supports both HTTP (dev) and HTTPS (prod)
- **Dependency Scanning**: ✅ GitHub Dependabot enabled; NuGet package vulnerability scanning in CI
- **Security Headers**: ✅ Implement security headers (X-Content-Type-Options, X-Frame-Options) via middleware
- **Audit Trail**: ✅ All manifest/blob modifications logged with timestamp and correlation ID

### Final Status: ✅ PASSED
All constitutional principles are satisfied after Phase 1 design. No violations requiring justification.

## Project Structure

### Documentation (this feature)

```text
specs/001-oci-registry-server/
├── plan.md              # This file (/speckit.plan command output)
├── research.md          # Phase 0 output - AWS S3 design patterns, OCI spec details
├── data-model.md        # Phase 1 output - Repository, Manifest, Blob, UploadSession entities
├── quickstart.md        # Phase 1 output - Getting started guide for developers
├── contracts/           # Phase 1 output - OpenAPI specification for OCI Distribution API
└── tasks.md             # Phase 2 output (/speckit.tasks command - NOT created by /speckit.plan)
```

### Source Code (repository root)

```text
src/
├── Dotreg.Api/                 # ASP.NET Core Web API project
│   ├── Controllers/            # API controllers for OCI endpoints
│   │   ├── ApiVersionController.cs       # GET /v2/
│   │   ├── ManifestsController.cs        # Manifest operations
│   │   ├── BlobsController.cs            # Blob operations
│   │   ├── UploadsController.cs          # Blob upload sessions
│   │   ├── TagsController.cs             # Tag listing
│   │   └── ReferrersController.cs        # Referrers API
│   ├── Middleware/             # Custom middleware
│   │   ├── ErrorHandlingMiddleware.cs    # OCI error response format
│   │   ├── RequestLoggingMiddleware.cs   # Structured logging
│   │   └── ValidationMiddleware.cs       # Repository/tag validation
│   ├── Models/                 # Request/response DTOs
│   │   ├── OciErrorResponse.cs
│   │   ├── TagList.cs
│   │   ├── Manifest.cs
│   │   └── UploadSession.cs
│   ├── Program.cs              # Application entry point
│   └── appsettings.json        # Configuration
│
├── Dotreg.Core/                # Core domain logic
│   ├── Services/               # Business logic services
│   │   ├── IRegistryService.cs
│   │   ├── RegistryService.cs
│   │   ├── IStorageService.cs
│   │   ├── S3StorageService.cs
│   │   ├── IUploadSessionManager.cs
│   │   └── UploadSessionManager.cs
│   ├── Models/                 # Domain entities
│   │   ├── Repository.cs
│   │   ├── Manifest.cs
│   │   ├── Blob.cs
│   │   ├── Tag.cs
│   │   ├── UploadSession.cs
│   │   └── Referrer.cs
│   ├── Validation/             # Business validation
│   │   ├── NameValidator.cs
│   │   ├── DigestValidator.cs
│   │   └── ContentValidator.cs
│   └── Exceptions/             # Domain exceptions
│       ├── ManifestNotFoundException.cs
│       ├── BlobNotFoundException.cs
│       └── DigestMismatchException.cs
│
└── Dotreg.Storage.S3/          # S3 storage implementation
    ├── S3StorageProvider.cs    # Core S3 operations
    ├── S3KeyBuilder.cs         # S3 key path generation
    ├── S3Config.cs             # S3 configuration
    └── Exceptions/
        └── S3StorageException.cs

tests/
├── Dotreg.Api.Tests/           # API integration tests
│   ├── ManifestEndpointTests.cs
│   ├── BlobEndpointTests.cs
│   ├── UploadEndpointTests.cs
│   ├── TagsEndpointTests.cs
│   └── ReferrersEndpointTests.cs
│
├── Dotreg.Core.Tests/          # Unit tests for business logic
│   ├── Services/
│   │   ├── RegistryServiceTests.cs
│   │   └── UploadSessionManagerTests.cs
│   ├── Validation/
│   │   ├── NameValidatorTests.cs
│   │   └── DigestValidatorTests.cs
│   └── Models/
│       └── UploadSessionTests.cs
│
├── Dotreg.Storage.S3.Tests/    # S3 storage unit tests
│   ├── S3StorageProviderTests.cs
│   └── S3KeyBuilderTests.cs
│
└── Dotreg.Integration.Tests/   # End-to-end tests with Docker client
    ├── DockerPullTests.cs
    ├── DockerPushTests.cs
    └── OrasTests.cs
```

**Structure Decision**: Web API project structure chosen for server application. Three-layer architecture:
1. **Dotreg.Api**: ASP.NET Core presentation layer with controllers and middleware
2. **Dotreg.Core**: Domain logic and business services (storage-agnostic)
3. **Dotreg.Storage.S3**: S3-specific storage implementation

This separation enables:
- Independent testing of business logic without S3 dependencies
- Future storage backend alternatives (if needed)
- Clean API layer focused on OCI protocol handling
- TDD-friendly design with clear boundaries

## Complexity Tracking

> **No violations - this section is empty**

All constitutional principles are satisfied. No complexity justifications required.

---

## Phase Completion Summary

### ✅ Phase 0: Outline & Research (COMPLETED)

**Deliverable**: `research.md`

**Research Topics Covered**:
1. AWS S3 as Primary Storage for OCI Registry - S3 key structure, lifecycle policies
2. .NET 9.0 and ASP.NET Core Best Practices - Clean architecture, streaming patterns
3. OCI Distribution Spec v1.1.1 Implementation Requirements - All mandatory/optional endpoints
4. Content Addressing and Digest Validation - SHA-256 calculation and validation
5. Upload Session Management - S3-based session state with expiration
6. Tag Management and Listing - Individual S3 objects with pagination
7. Referrers API Implementation - Image index storage with filtering
8. Testing Strategy with LocalStack - Testcontainers integration
9. Performance Optimization Strategies - Streaming, caching, concurrency
10. Configuration and Deployment - Environment-based config, Kubernetes deployment

**Key Decisions Made**:
- Use S3 hierarchical key structure (no database required)
- Three-layer clean architecture (Api, Core, Storage.S3)
- Streaming for large blobs to avoid memory exhaustion
- LocalStack with Testcontainers for integration testing
- TDD approach with xUnit and FluentAssertions

### ✅ Phase 1: Design & Contracts (COMPLETED)

**Deliverables**:
- `data-model.md` - Entity design (Repository, Manifest, Blob, Tag, UploadSession, Referrer)
- `contracts/openapi.yaml` - OpenAPI 3.0 specification with all OCI Distribution API endpoints
- `quickstart.md` - Developer getting started guide
- `.github/copilot-instructions.md` - Updated with C# .NET 9.0 and AWS S3 technologies

**Data Model Summary**:
- 6 core entities defined with properties, validation rules, storage representation
- S3 key patterns documented for all entity types
- State transitions and lifecycle documented
- OCI error response format and error codes cataloged

**API Contract Summary**:
- 11 endpoints documented (Base, Manifests, Blobs, Uploads, Tags, Referrers)
- All request/response schemas defined
- HTTP headers, status codes, and error responses specified
- Complete alignment with OCI Distribution Spec v1.1.1

**Quickstart Guide Summary**:
- Prerequisites and installation steps
- LocalStack setup for local S3 development
- Build, run, and test commands
- Docker CLI and ORAS CLI usage examples
- TDD workflow and common development tasks
- Troubleshooting section

### 📋 Phase 2: Task Breakdown (PENDING)

**Next Step**: Run `/speckit.tasks` command to generate `tasks.md`

This will create:
- Granular implementation tasks for each user story
- Test creation tasks (TDD - write tests first)
- Dependency order and task sequencing
- Effort estimates and acceptance criteria

---

## Next Actions

1. **Review Generated Artifacts**:
   - Review `research.md` for technical decisions
   - Review `data-model.md` for entity design
   - Review `contracts/openapi.yaml` for API specification
   - Review `quickstart.md` for development setup

2. **Run Phase 2 Command**:
   ```bash
   /speckit.tasks
   ```
   This will generate the task breakdown (`tasks.md`) for implementation.

3. **Begin Implementation** (after Phase 2 tasks are generated):
   - Set up .NET solution structure
   - Initialize LocalStack for development
   - Follow TDD workflow: write tests first, then implement
   - Start with User Story 1 (Pull) as highest priority

---

**Plan Generation Status**: ✅ **COMPLETE** (Phases 0-1)  
**Branch**: `001-oci-registry-server`  
**Generated Files**: 
- `specs/001-oci-registry-server/plan.md` (this file)
- `specs/001-oci-registry-server/research.md`
- `specs/001-oci-registry-server/data-model.md`
- `specs/001-oci-registry-server/contracts/openapi.yaml`
- `specs/001-oci-registry-server/quickstart.md`
- `.github/copilot-instructions.md` (updated)

**Ready for**: Task breakdown generation (`/speckit.tasks` command)
