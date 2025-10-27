# Feature Specification: OCI-Compliant Registry Server (dotreg)

**Feature Branch**: `001-oci-registry-server`  
**Created**: October 27, 2025  
**Status**: Draft  
**Input**: User description: "Build an OCI-compliant registry server named dotreg, strictly implementing all APIs defined by https://github.com/opencontainers/distribution-spec/blob/v1.1.1/spec.md and compatible with https://github.com/opencontainers/image-spec/blob/v1.1.1/spec.md so that docker (and other 3rd party tools like ORAS) and pull and push image to this registry."

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Pull Container Images (Priority: P1)

A developer wants to pull an existing container image from the dotreg registry to run it locally or deploy it to their environment. This is the most fundamental operation that any registry must support for basic functionality.

**Why this priority**: This is the minimum viable functionality for any container registry. Without pull capability, the registry cannot serve its primary purpose of distributing container images. This enables read-only registry usage, which is sufficient for many deployment scenarios.

**Independent Test**: Can be fully tested by pushing a sample image using any compliant tool, then performing a `docker pull` command against dotreg and successfully running the pulled container. Delivers immediate value by enabling image distribution.

**Acceptance Scenarios**:

1. **Given** a manifest exists in the registry at `myrepo/myimage:v1.0`, **When** a client requests GET `/v2/myrepo/myimage/manifests/v1.0`, **Then** the registry returns HTTP 200 with the manifest JSON and correct Content-Type header
2. **Given** a blob exists in the registry with digest `sha256:abc123`, **When** a client requests GET `/v2/myrepo/myimage/blobs/sha256:abc123`, **Then** the registry returns HTTP 200 with the blob content and correct Content-Length header
3. **Given** a multi-layer container image exists in the registry, **When** a client pulls the image using `docker pull dotreg.local/myrepo/myimage:v1.0`, **Then** all layers are downloaded successfully and the image is runnable
4. **Given** a manifest does not exist, **When** a client requests GET `/v2/myrepo/nonexistent/manifests/latest`, **Then** the registry returns HTTP 404 Not Found
5. **Given** a client performs a HEAD request to `/v2/myrepo/myimage/manifests/v1.0`, **When** the manifest exists, **Then** the registry returns HTTP 200 with Docker-Content-Digest and Content-Length headers without body

---

### User Story 2 - Push Container Images (Priority: P2)

A developer builds a container image locally and wants to push it to the dotreg registry for storage and distribution to other team members or deployment environments.

**Why this priority**: Push capability transforms dotreg from a read-only distribution point into a full registry service. This enables the complete CI/CD workflow where images are built, stored, and distributed. Essential for development teams but can be deferred if only distribution is initially needed.

**Independent Test**: Can be fully tested by performing a `docker push` command with a locally built image to dotreg, then verifying the image can be pulled back and matches the original. Delivers value by enabling teams to store and version their container images.

**Acceptance Scenarios**:

1. **Given** a client wants to push a blob, **When** they POST to `/v2/myrepo/myimage/blobs/uploads/`, **Then** the registry returns HTTP 202 with a Location header containing a unique upload session URL
2. **Given** an upload session exists, **When** a client PUT blobs content to the session URL with digest parameter, **Then** the registry returns HTTP 201 with the blob's pullable Location
3. **Given** a client wants to push a large blob in chunks, **When** they POST to initiate, PATCH multiple chunks, and PUT to finalize, **Then** the registry accepts all chunks in order and returns HTTP 201 on completion
4. **Given** all required blobs exist, **When** a client PUT a manifest to `/v2/myrepo/myimage/manifests/v1.0`, **Then** the registry returns HTTP 201 and the manifest becomes pullable
5. **Given** a blob upload is interrupted, **When** the client resumes by GETting the upload session URL, **Then** the registry returns HTTP 204 with Range header indicating uploaded bytes
6. **Given** a client performs `docker push dotreg.local/myrepo/myimage:v1.0`, **When** the push completes successfully, **Then** all layers and manifest are stored and the image is immediately pullable

---

### User Story 3 - Discover Available Content (Priority: P3)

A user wants to explore what images and tags are available in a repository to find the correct version to pull or to audit registry contents.

**Why this priority**: Content discovery enhances usability but is not required for basic push/pull workflows. Teams can function with direct knowledge of image names and tags. This is a quality-of-life feature that becomes more valuable as the registry grows.

**Independent Test**: Can be fully tested by pushing several tagged images to a repository, then calling the tags list API and verifying all tags are returned in lexical order. Delivers value by enabling users to explore available content without external documentation.

**Acceptance Scenarios**:

1. **Given** a repository `myrepo/myimage` has tags `v1.0`, `v1.1`, `latest`, **When** a client requests GET `/v2/myrepo/myimage/tags/list`, **Then** the registry returns HTTP 200 with JSON containing all tags in lexical order: `["latest", "v1.0", "v1.1"]`
2. **Given** a repository has 100 tags, **When** a client requests GET `/v2/myrepo/myimage/tags/list?n=10`, **Then** the registry returns HTTP 200 with 10 tags and a Link header for pagination
3. **Given** a client requests tags with pagination using `last` parameter, **When** they request `/v2/myrepo/myimage/tags/list?n=5&last=v1.0`, **Then** the registry returns up to 5 tags after `v1.0` (non-inclusive)
4. **Given** a repository has no tags, **When** a client requests GET `/v2/myrepo/empty/tags/list`, **Then** the registry returns HTTP 200 with an empty tags array
5. **Given** a repository does not exist, **When** a client requests GET `/v2/nonexistent/tags/list`, **Then** the registry returns HTTP 404 Not Found

---

### User Story 4 - Manage Content Lifecycle (Priority: P4)

An administrator wants to delete old or unused images, tags, and blobs to manage storage space and comply with retention policies.

**Why this priority**: Content management is important for long-term registry operation but not required for initial deployment. Teams can operate with append-only storage initially. This becomes critical as storage costs grow.

**Independent Test**: Can be fully tested by pushing an image, deleting its manifest or tag via DELETE request, then verifying subsequent GET requests return 404. Delivers value by enabling storage management and compliance with data retention policies.

**Acceptance Scenarios**:

1. **Given** a manifest exists at digest `sha256:abc123`, **When** an admin performs DELETE `/v2/myrepo/myimage/manifests/sha256:abc123`, **Then** the registry returns HTTP 202 and subsequent GET requests return 404
2. **Given** a tag `v1.0` points to a manifest, **When** an admin performs DELETE `/v2/myrepo/myimage/manifests/v1.0`, **Then** the registry returns HTTP 202 and the tag is removed but the manifest remains accessible by digest
3. **Given** a blob exists with digest `sha256:def456`, **When** an admin performs DELETE `/v2/myrepo/myimage/blobs/sha256:def456`, **Then** the registry returns HTTP 202 and subsequent blob pulls return 404
4. **Given** deletion is disabled in registry configuration, **When** a client attempts DELETE on any resource, **Then** the registry returns HTTP 405 Method Not Allowed or 400 Bad Request
5. **Given** a manifest or blob does not exist, **When** a client attempts DELETE, **Then** the registry returns HTTP 404 Not Found

---

### User Story 5 - Support Artifact Referrers (Priority: P5)

A user wants to attach metadata artifacts (signatures, SBOMs, scan results) to container images using the OCI referrers API to maintain associations between images and their related artifacts.

**Why this priority**: Referrers API is a newer feature for advanced artifact management. Basic registry functionality works without it. This enables modern supply chain security practices but can be added incrementally.

**Independent Test**: Can be fully tested by pushing a manifest with a `subject` field, then querying the referrers API for that digest and verifying the artifact appears in the returned list. Delivers value by enabling artifact supply chain scenarios.

**Acceptance Scenarios**:

1. **Given** a manifest with `subject` field pointing to digest `sha256:target123` is pushed, **When** a client GET `/v2/myrepo/referrers/sha256:target123`, **Then** the registry returns HTTP 200 with an image index containing the referrer's descriptor
2. **Given** multiple artifacts reference the same subject, **When** a client queries the referrers API, **Then** all referrers are returned in the image index manifest list
3. **Given** a client filters referrers by artifactType, **When** they request `/v2/myrepo/referrers/sha256:target123?artifactType=application/vnd.example.sbom.v1`, **Then** only matching artifacts are returned and response includes `OCI-Filters-Applied: artifactType` header
4. **Given** the referrers API is supported, **When** a manifest with `subject` is pushed, **Then** the registry response includes `OCI-Subject: <digest>` header
5. **Given** a manifest has no referrers, **When** a client queries its referrers, **Then** the registry returns HTTP 200 with an empty manifest list
6. **Given** the referrers API returns 404 (not supported), **When** a client queries referrers, **Then** the client falls back to the referrers tag schema `sha256-<digest>` and receives an image index if it exists

---

### User Story 6 - Cross-Repository Blob Mounting (Priority: P6)

A developer wants to push an image that shares layers with another image already in the registry, and wants to avoid re-uploading identical blobs to save time and bandwidth.

**Why this priority**: Blob mounting is an optimization that improves push performance but is not required for basic functionality. Images can be pushed without this feature, just less efficiently. Valuable for large images or bandwidth-constrained environments.

**Independent Test**: Can be fully tested by pushing an image, then pushing a second image to a different repository that shares layers, using the mount parameter to reference existing blobs. Delivers value by reducing upload time and storage overhead.

**Acceptance Scenarios**:

1. **Given** a blob with digest `sha256:shared123` exists in repository `repo-a`, **When** a client POST `/v2/repo-b/blobs/uploads/?mount=sha256:shared123&from=repo-a`, **Then** the registry returns HTTP 201 with Location header pointing to the mounted blob
2. **Given** a mount request references a non-existent blob, **When** the registry cannot find the blob, **Then** the registry returns HTTP 202 to allow the client to upload the blob normally
3. **Given** a mount request omits the `from` parameter, **When** the registry supports cross-mounting without explicit source, **Then** the registry searches for the blob across repositories and mounts if found
4. **Given** a registry does not support blob mounting, **When** a mount request is made, **Then** the registry returns HTTP 202 and the client proceeds with standard upload

---

### Edge Cases

- What happens when a client pushes a manifest that references non-existent blobs? The registry MUST reject with HTTP 4xx and `MANIFEST_BLOB_UNKNOWN` error code.
- How does the system handle concurrent uploads to the same blob? The registry accepts both uploads and stores one copy identified by digest; first completion wins.
- What happens when a client provides a digest that doesn't match the uploaded content? The registry MUST return HTTP 400 with `DIGEST_INVALID` error code.
- How does the registry handle manifests exceeding size limits? The registry MUST return HTTP 413 Payload Too Large if manifest exceeds configured limit (minimum 4MB support required).
- What happens when a tag name exceeds 128 characters or contains invalid characters? The registry MUST return HTTP 400 with `NAME_INVALID` error code.
- How does the system handle partial blob uploads that are abandoned? Upload sessions should expire after a configurable timeout (reasonable default: 24 hours).
- What happens when two clients try to push to the same tag simultaneously? Last write wins; no conflict detection required for tag updates.
- How does the registry handle Range requests for large blobs? The registry SHOULD support HTTP Range requests per RFC 9110 for resumable downloads.
- What happens when a client requests a manifest by digest but provides the wrong digest? The registry returns the manifest but client SHOULD verify digest matches and reject if mismatched.
- How does the system handle the referrers tag schema when referrers API is unavailable? Clients MUST update the image index at tag `sha256-<digest>` to maintain the referrers list manually.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: Registry MUST respond with HTTP 200 to GET `/v2/` to indicate OCI Distribution Spec v1.1.1 conformance
- **FR-002**: Registry MUST support pulling manifests via GET `/v2/<name>/manifests/<reference>` where reference is either a tag or digest
- **FR-003**: Registry MUST support pulling blobs via GET `/v2/<name>/blobs/<digest>`
- **FR-004**: Registry MUST support HEAD requests to check existence of manifests and blobs without transferring content
- **FR-005**: Registry MUST return HTTP 404 Not Found for non-existent manifests and blobs
- **FR-006**: Registry MUST validate repository names against regex `[a-z0-9]+((\.|_|__|-+)[a-z0-9]+)*(\/[a-z0-9]+((\.|_|__|-+)[a-z0-9]+)*)*`
- **FR-007**: Registry MUST validate tag names against regex `[a-zA-Z0-9_][a-zA-Z0-9._-]{0,127}` with maximum 128 characters
- **FR-008**: Registry MUST return manifests with Content-Type header matching the stored manifest's media type
- **FR-009**: Registry MUST return Docker-Content-Digest header in successful manifest and blob responses
- **FR-010**: Registry MUST support blob upload initiation via POST `/v2/<name>/blobs/uploads/` returning HTTP 202 with Location header
- **FR-011**: Registry MUST support monolithic blob upload via POST then PUT sequence
- **FR-012**: Registry SHOULD support single POST blob upload via POST `/v2/<name>/blobs/uploads/?digest=<digest>` with body
- **FR-013**: Registry MUST support chunked blob upload via POST, PATCH(es), and PUT sequence
- **FR-014**: Registry MUST validate Content-Range headers in chunked uploads and return HTTP 416 for out-of-order chunks
- **FR-015**: Registry MUST support resuming interrupted uploads via GET to upload session URL returning HTTP 204 with Range header
- **FR-016**: Registry MUST support manifest upload via PUT `/v2/<name>/manifests/<reference>` returning HTTP 201
- **FR-017**: Registry MUST validate uploaded blob digests match provided digest parameter and return HTTP 400 `DIGEST_INVALID` if mismatch
- **FR-018**: Registry MUST store manifests in exact byte representation provided by client
- **FR-019**: Registry MUST reject manifests referencing non-existent blobs with HTTP 4xx and `MANIFEST_BLOB_UNKNOWN` error code (registry MAY choose to accept initially)
- **FR-020**: Registry MUST support listing tags via GET `/v2/<name>/tags/list` returning HTTP 200 with JSON array in lexical order
- **FR-021**: Registry MUST support paginated tag listing via `n` and `last` query parameters
- **FR-022**: Registry MUST include Link header with `rel="next"` when additional tags are available beyond requested page
- **FR-023**: Registry SHOULD support HTTP Range requests for blob downloads per RFC 9110
- **FR-024**: Registry SHOULD support blob mounting via POST `/v2/<name>/blobs/uploads/?mount=<digest>&from=<other_name>`
- **FR-025**: Registry MAY support manifest and blob deletion via DELETE requests (returns HTTP 202 Accepted if enabled, HTTP 405/400 if disabled)
- **FR-026**: Registry MUST support referrers API via GET `/v2/<name>/referrers/<digest>` returning HTTP 200 with image index (if referrers API is enabled)
- **FR-027**: Registry SHOULD support filtering referrers by artifactType query parameter
- **FR-028**: Registry MUST include `OCI-Filters-Applied: artifactType` header when artifactType filtering is applied
- **FR-029**: Registry MUST include `OCI-Subject: <digest>` response header when processing manifest with subject field (if referrers API enabled)
- **FR-030**: Registry MUST return empty image index for referrers queries with no matches (not 404)
- **FR-031**: Registry MUST enforce maximum manifest size of at least 4 megabytes
- **FR-032**: Registry MUST return HTTP 413 Payload Too Large for manifests exceeding configured size limit
- **FR-033**: Registry MUST return error responses in JSON format with `errors` array containing `code`, `message`, and optional `detail` fields
- **FR-034**: Registry MUST use standard error codes: BLOB_UNKNOWN, BLOB_UPLOAD_INVALID, BLOB_UPLOAD_UNKNOWN, DIGEST_INVALID, MANIFEST_BLOB_UNKNOWN, MANIFEST_INVALID, MANIFEST_UNKNOWN, NAME_INVALID, NAME_UNKNOWN, SIZE_INVALID, UNAUTHORIZED, DENIED, UNSUPPORTED, TOOMANYREQUESTS
- **FR-035**: Registry MAY return informational warnings in Warning headers (HTTP 299 warn-code) limited to 4096 bytes total
- **FR-036**: Registry MUST be compatible with Docker CLI, ORAS, and other OCI-compliant client tools
- **FR-037**: Registry MUST handle concurrent blob uploads to the same digest by storing only one copy (content-addressed storage)
- **FR-038**: Registry SHOULD support Content-Length validation for uploaded blobs
- **FR-039**: Registry MUST ignore parameters on Content-Type headers in requests and SHOULD NOT include parameters in response Content-Type headers
- **FR-040**: Registry MAY support authentication and authorization but mechanism is implementation-defined

### Key Entities

- **Repository**: A namespace scope containing related manifests, blobs, and tags (e.g., `myorg/myapp`). Identified by name matching validation regex. Contains zero or more manifests and blobs.
- **Manifest**: A JSON document describing an image or artifact, containing references to blobs (layers, config) and metadata. Identified by digest (SHA-256 or other algorithm). Can be referenced by zero or more tags. Has a media type (e.g., `application/vnd.oci.image.manifest.v1+json`). May have a subject field pointing to another manifest.
- **Blob**: Binary content stored in registry, content-addressed by digest. Represents image layers, configurations, or other data. Immutable once stored. Can be referenced by multiple manifests across repositories.
- **Tag**: Human-readable mutable pointer to a manifest (e.g., `latest`, `v1.0`). Maximum 128 characters. Can be reassigned to different manifests. Optional - manifests can exist without tags.
- **Digest**: Cryptographic hash uniquely identifying content (e.g., `sha256:abc123...`). Used to address blobs and manifests. Ensures content integrity.
- **Upload Session**: Temporary state for multi-part blob upload. Identified by UUID in Location URL. Tracks uploaded byte ranges. Expires after timeout (recommended: 24 hours).
- **Referrer**: A manifest that references another manifest via subject field. Forms artifact-to-image relationships (signatures, SBOMs). Listed via referrers API or tag schema.
- **Image Index**: A manifest type containing list of manifests (multi-platform images or referrers list). Media type `application/vnd.oci.image.index.v1+json`.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Docker clients can successfully pull and run multi-layer container images from dotreg without errors in under 30 seconds for images up to 500MB
- **SC-002**: Docker clients can successfully push multi-layer container images to dotreg with all layers and manifest stored correctly within 60 seconds for images up to 500MB
- **SC-003**: Registry passes 100% of mandatory OCI Distribution Spec v1.1.1 conformance tests using the official conformance testing tool
- **SC-004**: Registry supports at least 100 concurrent pull operations without performance degradation (response time remains under 2 seconds for manifest requests)
- **SC-005**: Registry supports at least 20 concurrent push operations without data corruption or failed uploads
- **SC-006**: ORAS CLI and other third-party OCI-compliant tools can successfully push and pull artifacts to/from dotreg
- **SC-007**: Tag listing returns complete results for repositories with up to 1000 tags within 2 seconds
- **SC-008**: Registry correctly handles and resumes interrupted blob uploads, allowing clients to resume from last uploaded byte without re-uploading completed chunks
- **SC-009**: Blob mounting reduces upload time by at least 50% when pushing images with shared layers compared to full upload
- **SC-010**: Registry maintains data integrity with 100% accuracy - all stored content matches provided digests
- **SC-011**: Referrers API returns complete artifact lists for images with up to 100 referrers within 2 seconds
- **SC-012**: Registry handles manifest deletions without affecting other manifests or tags, with deleted content returning 404 within 1 second
- **SC-013**: Error responses provide actionable error codes and messages that enable clients to diagnose and resolve issues without manual intervention 90% of the time
