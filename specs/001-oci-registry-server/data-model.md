# Data Model: OCI Registry (dotreg)

**Date**: October 27, 2025  
**Feature**: OCI-Compliant Registry Server  
**Storage**: AWS S3 (no SQL/NoSQL database)

---

## Overview

The dotreg registry data model consists of content-addressed entities stored in AWS S3. All entities are immutable except Tags (which are mutable pointers). The model strictly follows OCI Distribution Spec v1.1.1 and OCI Image Spec v1.1.1.

**Key Design Principles**:
- **Content Addressing**: Blobs and Manifests identified by cryptographic digest (SHA-256)
- **Immutability**: Once stored, blobs and manifests cannot be modified (only deleted if enabled)
- **Mutable Pointers**: Tags provide human-readable, mutable references to manifests
- **No Database**: All state stored in S3 as objects (JSON or binary)

---

## Entity Relationships

```
Repository (1) ──────> (N) Manifest
                           │
                           ├──> (N) Blob (via references)
                           └──> (N) Tag (mutable pointers)

Manifest (subject) <────(N) Referrer Manifest
```

---

## 1. Repository

**Description**: A namespace containing related manifests, blobs, and tags (e.g., `myorg/myapp`, `library/nginx`).

### Properties

| Property | Type | Required | Validation | Description |
|----------|------|----------|------------|-------------|
| Name | `string` | ✅ | Regex: `[a-z0-9]+((\.|_|__|-+)[a-z0-9]+)*(\/[a-z0-9]+((\.|_|__|-+)[a-z0-9]+)*)*` | Repository identifier (e.g., `myorg/myapp`) |

### Validation Rules

- **Name format**: Must match OCI repository name pattern
- **Lowercase only**: All letters must be lowercase
- **Path separators**: Forward slash `/` for multi-level namespaces
- **Special characters**: Only `.`, `_`, `__`, `-` allowed as separators between segments
- **No leading/trailing separators**: Name cannot start or end with `/`, `.`, `_`, or `-`

### Storage Representation

Repositories are **logical entities** - they don't have a dedicated S3 object. A repository exists implicitly when it contains at least one manifest or blob.

**S3 Key Prefix**: `/repositories/{repository-name}/`

**Example**:
```
/repositories/myorg/myapp/manifests/sha256:abc123...
/repositories/myorg/myapp/blobs/sha256:def456...
/repositories/myorg/myapp/tags/v1.0
```

### State Transitions

- **Created**: Implicitly created when first manifest or blob is stored
- **Deleted**: Implicitly deleted when all manifests, blobs, and tags are removed

---

## 2. Manifest

**Description**: A JSON document describing an image or artifact. Contains references to blobs (layers, config) and metadata.

### Properties

| Property | Type | Required | Validation | Description |
|----------|------|----------|------------|-------------|
| Digest | `string` | ✅ | Format: `{algorithm}:{hex}` (e.g., `sha256:abc123...`) | Content-addressed identifier |
| MediaType | `string` | ✅ | OCI media types (see below) | Manifest type identifier |
| Content | `byte[]` | ✅ | JSON format, max 4MB (default) | Raw manifest JSON bytes |
| Size | `long` | ✅ | Must match actual content size | Size in bytes |
| Subject | `string` | ❌ | Format: `{algorithm}:{hex}` | Optional reference to another manifest (for referrers) |

### Media Types

**Supported manifest types**:
- `application/vnd.oci.image.manifest.v1+json` - Single image
- `application/vnd.oci.image.index.v1+json` - Multi-platform image or referrers list
- `application/vnd.docker.distribution.manifest.v2+json` - Docker v2 manifest
- `application/vnd.docker.distribution.manifest.list.v2+json` - Docker manifest list

### Validation Rules

- **Digest calculation**: SHA-256 hash of entire JSON content (byte-for-byte)
- **JSON validity**: Must be valid JSON
- **Size limit**: Must not exceed configured maximum (default 4MB, minimum per spec)
- **Blob references**: All referenced blobs must exist (MAY be checked, registry-dependent)
- **Immutability**: Once stored, content cannot be changed
- **Media type**: Must be present and match stored value on retrieval

### Storage Representation

**S3 Key**: `/repositories/{repository-name}/manifests/{digest}`

**S3 Metadata**:
```json
{
  "content-type": "application/vnd.oci.image.manifest.v1+json",
  "x-dotreg-size": "1234",
  "x-dotreg-uploaded-at": "2025-10-27T10:00:00Z",
  "x-dotreg-subject": "sha256:target123..."  // If present
}
```

**Example Manifest Content** (stored as-is):
```json
{
  "schemaVersion": 2,
  "mediaType": "application/vnd.oci.image.manifest.v1+json",
  "config": {
    "mediaType": "application/vnd.oci.image.config.v1+json",
    "size": 7023,
    "digest": "sha256:config123..."
  },
  "layers": [
    {
      "mediaType": "application/vnd.oci.image.layer.v1.tar+gzip",
      "size": 32654,
      "digest": "sha256:layer1..."
    },
    {
      "mediaType": "application/vnd.oci.image.layer.v1.tar+gzip",
      "size": 16724,
      "digest": "sha256:layer2..."
    }
  ],
  "subject": {
    "mediaType": "application/vnd.oci.image.manifest.v1+json",
    "size": 1234,
    "digest": "sha256:target123..."
  }
}
```

### State Transitions

- **Created**: Stored via `PUT /v2/{name}/manifests/{reference}`
- **Retrieved**: Via `GET /v2/{name}/manifests/{reference}` (by tag or digest)
- **Deleted**: Via `DELETE /v2/{name}/manifests/{digest}` (if deletion enabled)

---

## 3. Blob

**Description**: Binary content stored in registry. Represents image layers, configurations, or other artifacts. Content-addressed and immutable.

### Properties

| Property | Type | Required | Validation | Description |
|----------|------|----------|------------|-------------|
| Digest | `string` | ✅ | Format: `{algorithm}:{hex}` | Content-addressed identifier |
| Size | `long` | ✅ | Must match actual content size | Size in bytes |
| Content | `byte[]` | ✅ | Binary data | Raw blob bytes |

### Validation Rules

- **Digest calculation**: SHA-256 hash of entire content
- **Digest validation**: Computed digest must match provided digest on upload
- **Immutability**: Once stored, content cannot be changed
- **Deduplication**: Same digest = same content (only one copy stored)

### Storage Representation

**S3 Key**: `/repositories/{repository-name}/blobs/{digest}`

**S3 Metadata**:
```json
{
  "content-type": "application/octet-stream",
  "x-dotreg-size": "32654",
  "x-dotreg-uploaded-at": "2025-10-27T10:00:00Z"
}
```

**Note**: Blobs can be shared across repositories (cross-repository blob mounting), but stored per-repository in this implementation. Future optimization could deduplicate at bucket level.

### State Transitions

- **Uploading**: During upload session (see UploadSession entity)
- **Created**: After successful upload completion
- **Retrieved**: Via `GET /v2/{name}/blobs/{digest}`
- **Deleted**: Via `DELETE /v2/{name}/blobs/{digest}` (if deletion enabled)

---

## 4. Tag

**Description**: Human-readable, mutable pointer to a manifest. Provides user-friendly names like `latest`, `v1.0`, `stable`.

### Properties

| Property | Type | Required | Validation | Description |
|----------|------|----------|------------|-------------|
| Name | `string` | ✅ | Regex: `[a-zA-Z0-9_][a-zA-Z0-9._-]{0,127}` | Tag identifier (max 128 chars) |
| Digest | `string` | ✅ | Format: `{algorithm}:{hex}` | Manifest digest this tag points to |
| UpdatedAt | `DateTime` | ✅ | ISO 8601 format | Last update timestamp |

### Validation Rules

- **Name format**: Must match OCI tag name pattern
- **Length**: Maximum 128 characters
- **First character**: Must be alphanumeric or underscore
- **Subsequent characters**: Alphanumeric, period, underscore, or hyphen
- **Target existence**: Manifest with referenced digest should exist (MAY be validated)
- **Mutability**: Tag can be reassigned to different manifest (last write wins)

### Storage Representation

**S3 Key**: `/repositories/{repository-name}/tags/{tag-name}`

**Content** (JSON):
```json
{
  "digest": "sha256:abc123...",
  "updatedAt": "2025-10-27T10:00:00Z"
}
```

### State Transitions

- **Created**: Via `PUT /v2/{name}/manifests/{tag}` (tag as reference)
- **Updated**: Same endpoint reassigns tag to new digest
- **Retrieved**: Via `GET /v2/{name}/manifests/{tag}` (resolves to manifest)
- **Listed**: Via `GET /v2/{name}/tags/list`
- **Deleted**: Via `DELETE /v2/{name}/manifests/{tag}` (tag is removed, manifest remains)

### Concurrent Updates

**Behavior**: Last write wins. No conflict detection or optimistic locking.

**Example**:
1. Client A: `PUT /v2/myrepo/manifests/latest` → `sha256:aaa...`
2. Client B: `PUT /v2/myrepo/manifests/latest` → `sha256:bbb...` (concurrent)
3. Result: Tag points to whichever write completed last

---

## 5. UploadSession

**Description**: Temporary state for multi-part blob upload. Tracks upload progress and manages chunked uploads.

### Properties

| Property | Type | Required | Validation | Description |
|----------|------|----------|------------|-------------|
| UUID | `string` | ✅ | UUIDv4 format | Unique session identifier |
| RepositoryName | `string` | ✅ | Repository name validation | Target repository |
| Digest | `string` | ❌ | Format: `{algorithm}:{hex}` | Expected digest (provided by client) |
| UploadedRanges | `List<Range>` | ✅ | Non-overlapping, ordered | Byte ranges successfully uploaded |
| TotalSize | `long` | ❌ | Must match actual size on completion | Expected total size (if known) |
| CreatedAt | `DateTime` | ✅ | ISO 8601 format | Session creation timestamp |
| ExpiresAt | `DateTime` | ✅ | ISO 8601 format | Session expiration (default: 24 hours) |
| S3UploadId | `string` | ✅ | S3 multipart upload ID | S3-specific upload identifier |

### Validation Rules

- **UUID uniqueness**: Each session has unique UUID
- **Range validation**: Uploaded ranges must not overlap
- **Range continuity**: Ranges must be contiguous on completion
- **Expiration**: Sessions expire after configured timeout (default: 24 hours)
- **Digest**: If provided by client, must be validated on completion

### Storage Representation

**S3 Key**: `/uploads/{uuid}/metadata.json`

**Content** (JSON):
```json
{
  "uuid": "550e8400-e29b-41d4-a716-446655440000",
  "repositoryName": "myorg/myapp",
  "digest": "sha256:abc123...",
  "uploadedRanges": [
    {"start": 0, "end": 1048575},
    {"start": 1048576, "end": 2097151}
  ],
  "totalSize": 10485760,
  "createdAt": "2025-10-27T10:00:00Z",
  "expiresAt": "2025-10-28T10:00:00Z",
  "s3UploadId": "abc123multipartid"
}
```

**S3 Multipart Upload**: Blob chunks stored as S3 multipart parts, managed by S3.

**S3 Lifecycle Policy**: Automatically deletes expired sessions after 24 hours.

### State Transitions

1. **Initiated**: `POST /v2/{name}/blobs/uploads/` → Session created
2. **Uploading**: `PATCH /v2/{name}/blobs/uploads/{uuid}` → Chunks uploaded
3. **Resuming**: `GET /v2/{name}/blobs/uploads/{uuid}` → Get progress
4. **Completed**: `PUT /v2/{name}/blobs/uploads/{uuid}?digest={digest}` → Blob finalized
5. **Expired**: After 24 hours → S3 lifecycle deletes session
6. **Abandoned**: Client stops uploading → Session expires automatically

---

## 6. Referrer

**Description**: A manifest that references another manifest via the `subject` field. Establishes artifact-to-image relationships (signatures, SBOMs, attestations).

### Properties

Referrer is a **specialized Manifest** with additional relationship:
- Inherits all Manifest properties
- **Subject** field is required (points to target manifest)
- **ArtifactType** describes the referrer type (e.g., `application/vnd.dev.cosign.simplesigning.v1+json`)

### Validation Rules

- All Manifest validation rules apply
- **Subject presence**: `subject` field must be present in manifest JSON
- **Subject format**: Must be valid descriptor with digest
- **Circular references**: Registry MAY reject manifests creating circular references

### Storage Representation

**Manifest storage**: Same as regular manifests (`/repositories/{name}/manifests/{digest}`)

**Referrers Index**: `/repositories/{name}/referrers/{subject-digest}/index.json`

**Index Content** (Image Index manifest):
```json
{
  "schemaVersion": 2,
  "mediaType": "application/vnd.oci.image.index.v1+json",
  "manifests": [
    {
      "mediaType": "application/vnd.oci.image.manifest.v1+json",
      "size": 1234,
      "digest": "sha256:signature123...",
      "artifactType": "application/vnd.dev.cosign.simplesigning.v1+json",
      "annotations": {
        "org.opencontainers.image.created": "2025-10-27T10:00:00Z"
      }
    },
    {
      "mediaType": "application/vnd.oci.image.manifest.v1+json",
      "size": 5678,
      "digest": "sha256:sbom456...",
      "artifactType": "application/vnd.example.sbom.v1+json"
    }
  ]
}
```

### State Transitions

1. **Referrer Manifest Pushed**: Client pushes manifest with `subject` field
2. **Index Updated**: Server reads existing referrers index for subject
3. **Referrer Added**: New referrer descriptor added to index
4. **Index Stored**: Updated index written back to S3
5. **Queried**: Via `GET /v2/{name}/referrers/{digest}` (returns index)
6. **Filtered**: Via `GET /v2/{name}/referrers/{digest}?artifactType={type}` (filtered index)

### Fallback: Tag Schema

**If referrers API is unavailable**, clients use tag-based schema:

**Tag name**: `sha256-{digest}` (where digest is hex part only, without `sha256:` prefix)

**Example**: For subject `sha256:abc123...`, tag is `sha256-abc123...`

**Content**: Same image index as above, but stored as a tag

---

## 7. Error Response

**Description**: Standardized error response format for all API errors.

### Properties

| Property | Type | Required | Description |
|----------|------|----------|-------------|
| Errors | `List<ErrorDetail>` | ✅ | Array of error objects |

### ErrorDetail Properties

| Property | Type | Required | Description |
|----------|------|----------|-------------|
| Code | `string` | ✅ | Error code (see error codes below) |
| Message | `string` | ✅ | Human-readable error message |
| Detail | `object` | ❌ | Additional context (structure varies by error) |

### Error Codes

| Code | Description | HTTP Status |
|------|-------------|-------------|
| `BLOB_UNKNOWN` | Blob not found | 404 |
| `BLOB_UPLOAD_INVALID` | Invalid blob upload (e.g., digest mismatch) | 400 |
| `BLOB_UPLOAD_UNKNOWN` | Upload session not found | 404 |
| `DIGEST_INVALID` | Provided digest is malformed or doesn't match content | 400 |
| `MANIFEST_BLOB_UNKNOWN` | Manifest references non-existent blob | 400 |
| `MANIFEST_INVALID` | Manifest is invalid (e.g., malformed JSON) | 400 |
| `MANIFEST_UNKNOWN` | Manifest not found | 404 |
| `NAME_INVALID` | Repository or tag name is invalid | 400 |
| `NAME_UNKNOWN` | Repository not found | 404 |
| `SIZE_INVALID` | Size mismatch or payload too large | 400 |
| `UNAUTHORIZED` | Authentication required | 401 |
| `DENIED` | Authorization failed | 403 |
| `UNSUPPORTED` | Operation not supported | 501 |
| `TOOMANYREQUESTS` | Rate limit exceeded | 429 |

### Example Error Response

```json
{
  "errors": [
    {
      "code": "MANIFEST_UNKNOWN",
      "message": "manifest unknown",
      "detail": {
        "repository": "myorg/myapp",
        "reference": "nonexistent-tag"
      }
    }
  ]
}
```

---

## 8. Pagination

**Description**: Standard pagination model for listing operations.

### Properties

| Property | Type | Required | Description |
|----------|------|----------|-------------|
| n | `int` | ❌ | Maximum number of results (default: 100, max: 1000) |
| last | `string` | ❌ | Last item from previous page (exclusive) |

### Response Headers

| Header | Required | Description |
|--------|----------|-------------|
| Link | Conditional | Next page URL (present if more results available) |

**Link Header Format**:
```
Link: </v2/{name}/tags/list?n=10&last=v1.0>; rel="next"
```

### Example Tag Listing Response

**Request**: `GET /v2/myorg/myapp/tags/list?n=3`

**Response**:
```json
{
  "name": "myorg/myapp",
  "tags": ["latest", "v1.0", "v1.1"]
}
```

**Headers**:
```
Link: </v2/myorg/myapp/tags/list?n=3&last=v1.1>; rel="next"
Content-Type: application/json
```

---

## Data Model Summary

| Entity | Mutability | Storage | Identifier | Lifetime |
|--------|-----------|---------|------------|----------|
| Repository | Implicit | S3 prefix | Name | Until all content deleted |
| Manifest | Immutable | S3 object | Digest (SHA-256) | Until deleted (if enabled) |
| Blob | Immutable | S3 object | Digest (SHA-256) | Until deleted (if enabled) |
| Tag | Mutable | S3 object (JSON) | Name | Until deleted or reassigned |
| UploadSession | Mutable | S3 object (JSON) | UUID | 24 hours (configurable) |
| Referrer | Immutable | S3 object (manifest) + index | Digest (SHA-256) | Until deleted (if enabled) |

**Total Entity Count**: 6 core entities + 1 error response model + 1 pagination model

**Storage Efficiency**: Content-addressed storage ensures automatic deduplication of identical blobs/manifests across the registry.
