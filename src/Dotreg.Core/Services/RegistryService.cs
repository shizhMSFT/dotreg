using Dotreg.Core.Exceptions;
using Dotreg.Core.Models;
using Dotreg.Core.Validation;
using Microsoft.Extensions.Configuration;

namespace Dotreg.Core.Services;

/// <summary>
/// Implementation of IRegistryService using IStorageService
/// </summary>
public class RegistryService : IRegistryService
{
    private readonly IStorageService _storage;
    private readonly IConfiguration? _configuration;

    public RegistryService(IStorageService storage, IConfiguration? configuration = null)
    {
        _storage = storage ?? throw new ArgumentNullException(nameof(storage));
        _configuration = configuration;
    }

    public async Task<Manifest> GetManifestAsync(string name, string reference, CancellationToken cancellationToken = default)
    {
        // Validate repository name
        if (!NameValidator.IsValidRepositoryName(name))
        {
            throw new InvalidNameException(name, "Invalid repository name format");
        }

        // Validate reference (tag or digest)
        if (!NameValidator.IsValidTagName(reference) && !DigestValidator.IsValidDigest(reference))
        {
            throw new InvalidNameException(reference, "Invalid reference format");
        }

        // Build storage key
        string key;
        string manifestDigest;
        
        if (reference.StartsWith("sha256:", StringComparison.Ordinal))
        {
            // Direct digest reference
            manifestDigest = reference;
            key = $"manifests/{name}/{manifestDigest}";
        }
        else
        {
            // Tag reference - resolve to digest via tag file
            key = $"tags/{name}/{reference}";
            if (!await _storage.ExistsAsync(key, cancellationToken))
            {
                throw new ManifestNotFoundException(name, reference);
            }
            
            // Read digest from tag file
            var tagData = await _storage.GetAsync(key, cancellationToken);
            manifestDigest = System.Text.Encoding.UTF8.GetString(tagData).Trim();
            key = $"manifests/{name}/{manifestDigest}";
        }

        // Get manifest data
        if (!await _storage.ExistsAsync(key, cancellationToken))
        {
            throw new ManifestNotFoundException(name, reference);
        }

        var content = await _storage.GetAsync(key, cancellationToken);
        var contentType = await _storage.GetContentTypeAsync(key, cancellationToken);
        var digest = DigestValidator.CalculateSha256(content);

        return new Manifest
        {
            Digest = digest,
            MediaType = contentType ?? "application/vnd.oci.image.manifest.v1+json",
            Content = content
        };
    }

    public async Task<Blob> GetBlobAsync(string name, string digest, CancellationToken cancellationToken = default)
    {
        // Validate repository name
        if (!NameValidator.IsValidRepositoryName(name))
        {
            throw new InvalidNameException(name, "Invalid repository name format");
        }

        // Validate digest format - this will throw ArgumentException if invalid
        DigestValidator.ValidateDigest(digest);

        // Build storage key - this should match S3KeyBuilder.BuildBlobKey format
        var key = $"repositories/{name}/blobs/{digest}";

        // Check if blob exists
        if (!await _storage.ExistsAsync(key, cancellationToken))
        {
            throw new BlobNotFoundException(name, digest);
        }

        // Get blob stream and metadata
        var stream = await _storage.GetStreamAsync(key, cancellationToken);
        var size = await _storage.GetSizeAsync(key, cancellationToken);

        return new Blob
        {
            Digest = digest,
            Size = size,
            Content = stream,
            MediaType = "application/octet-stream"
        };
    }

    public async Task<bool> CheckManifestExistsAsync(string name, string reference, CancellationToken cancellationToken = default)
    {
        // Validate repository name
        if (!NameValidator.IsValidRepositoryName(name))
        {
            return false;
        }

        // Validate reference
        if (!NameValidator.IsValidTagName(reference) && !DigestValidator.IsValidDigest(reference))
        {
            return false;
        }

        // Build storage key
        string key;
        if (reference.StartsWith("sha256:", StringComparison.Ordinal))
        {
            key = $"manifests/{name}/{reference}";
        }
        else
        {
            // Tag reference - check if tag exists
            key = $"tags/{name}/{reference}";
            if (!await _storage.ExistsAsync(key, cancellationToken))
            {
                return false;
            }
            
            // Read digest and check manifest
            var tagData = await _storage.GetAsync(key, cancellationToken);
            var manifestDigest = System.Text.Encoding.UTF8.GetString(tagData).Trim();
            key = $"manifests/{name}/{manifestDigest}";
        }

        return await _storage.ExistsAsync(key, cancellationToken);
    }

    public async Task<bool> CheckBlobExistsAsync(string name, string digest, CancellationToken cancellationToken = default)
    {
        // Validate repository name and digest
        if (!NameValidator.IsValidRepositoryName(name) || !DigestValidator.IsValidDigest(digest))
        {
            return false;
        }

        var key = $"repositories/{name}/blobs/{digest}";
        return await _storage.ExistsAsync(key, cancellationToken);
}

    public async Task<string> PutManifestAsync(string name, string reference, byte[] content, string contentType, CancellationToken cancellationToken = default)
    {
        // Validate repository name
        if (!NameValidator.IsValidRepositoryName(name))
        {
            throw new InvalidNameException(name, "Invalid repository name format");
        }

        // Calculate digest
        var digest = DigestValidator.CalculateSha256(content);

        // Store manifest by digest
        var manifestKey = $"manifests/{name}/{digest}";
        await _storage.PutAsync(manifestKey, content, contentType, cancellationToken);

        // If reference is a tag (not a digest), create/update tag file
        if (!reference.StartsWith("sha256:", StringComparison.Ordinal))
        {
            if (!NameValidator.IsValidTagName(reference))
            {
                throw new InvalidNameException(reference, "Invalid tag name format");
            }

            var tagKey = $"tags/{name}/{reference}";
            var tagContent = System.Text.Encoding.UTF8.GetBytes(digest);
            await _storage.PutAsync(tagKey, tagContent, "text/plain", cancellationToken);
        }

        return digest;
    }

    public async Task<List<string>> ListTagsAsync(string name, int maxResults, string? startAfter, CancellationToken cancellationToken = default)
    {
        // Validate repository name
        if (!NameValidator.IsValidRepositoryName(name))
        {
            throw new InvalidNameException(name, "Invalid repository name format");
        }

        // List all tag keys with the prefix
        var prefix = $"tags/{name}/";
        var keys = await _storage.ListKeysAsync(prefix, cancellationToken);

        // Extract tag names from keys
        var tags = keys
            .Select(key => key.Substring(prefix.Length))
            .OrderBy(tag => tag, StringComparer.Ordinal)
            .ToList();

        // Apply startAfter filter
        if (!string.IsNullOrEmpty(startAfter))
        {
            tags = tags.Where(tag => string.CompareOrdinal(tag, startAfter) > 0).ToList();
        }

        // Apply maxResults limit
        if (tags.Count > maxResults)
        {
            tags = tags.Take(maxResults).ToList();
        }

        return tags;
    }

    public async Task DeleteManifestAsync(string name, string reference, CancellationToken cancellationToken = default)
    {
        // Check if deletion is enabled
        var deletionEnabledStr = _configuration?["Registry:EnableDeletion"];
        var deletionEnabled = !string.IsNullOrEmpty(deletionEnabledStr) && bool.Parse(deletionEnabledStr);
        if (!deletionEnabled)
        {
            throw new InvalidOperationException("Manifest deletion is disabled. Set Registry:EnableDeletion to true to enable.");
        }

        // Validate repository name
        if (!NameValidator.IsValidRepositoryName(name))
        {
            throw new InvalidNameException(name, "Invalid repository name format");
        }

        // Validate reference - must be digest, not tag
        if (!DigestValidator.IsValidDigest(reference))
        {
            throw new InvalidNameException(reference, "Deletion requires digest reference, not tag name");
        }

        var key = $"manifests/{name}/{reference}";
        
        if (!await _storage.ExistsAsync(key, cancellationToken))
        {
            throw new ManifestNotFoundException(name, reference);
        }

        await _storage.DeleteAsync(key, cancellationToken);
    }

    public async Task DeleteBlobAsync(string name, string digest, CancellationToken cancellationToken = default)
    {
        // Check if deletion is enabled
        var deletionEnabledStr = _configuration?["Registry:EnableDeletion"];
        var deletionEnabled = !string.IsNullOrEmpty(deletionEnabledStr) && bool.Parse(deletionEnabledStr);
        if (!deletionEnabled)
        {
            throw new InvalidOperationException("Blob deletion is disabled. Set Registry:EnableDeletion to true to enable.");
        }

        // Validate repository name
        if (!NameValidator.IsValidRepositoryName(name))
        {
            throw new InvalidNameException(name, "Invalid repository name format");
        }

        // Validate digest
        if (!DigestValidator.IsValidDigest(digest))
        {
            throw new InvalidNameException(digest, "Invalid digest format");
        }

        var key = $"repositories/{name}/blobs/{digest}";
        
        if (!await _storage.ExistsAsync(key, cancellationToken))
        {
            throw new BlobNotFoundException(name, digest);
        }

        await _storage.DeleteAsync(key, cancellationToken);
    }

    public async Task<List<ReferrerDescriptor>> GetReferrersAsync(string name, string digest, string? artifactType, CancellationToken cancellationToken = default)
    {
        // Check if referrers API is enabled
        var enableReferrersApi = bool.Parse(_configuration?["Registry:EnableReferrersApi"] ?? "false");
        if (!enableReferrersApi)
        {
            throw new InvalidOperationException("Referrers API is disabled. Enable it by setting Registry:EnableReferrersApi to true in configuration.");
        }

        // Validate repository name
        if (!NameValidator.IsValidRepositoryName(name))
        {
            throw new InvalidNameException(name, "Invalid repository name format");
        }

        // Validate digest format
        DigestValidator.ValidateDigest(digest);

        // Build storage key for referrers index
        var key = $"referrers/{name}/{digest}/index.json";

        // Check if referrers index exists
        if (!await _storage.ExistsAsync(key, cancellationToken))
        {
            // No referrers - return empty list (not 404)
            return new List<ReferrerDescriptor>();
        }

        // Read and parse referrers index
        var indexStream = await _storage.GetStreamAsync(key, cancellationToken);
        var indexDoc = await System.Text.Json.JsonDocument.ParseAsync(indexStream, cancellationToken: cancellationToken);

        var referrers = new List<ReferrerDescriptor>();
        if (indexDoc.RootElement.TryGetProperty("manifests", out var manifests))
        {
            foreach (var manifest in manifests.EnumerateArray())
            {
                var referrer = new ReferrerDescriptor
                {
                    MediaType = manifest.GetProperty("mediaType").GetString()!,
                    Digest = manifest.GetProperty("digest").GetString()!,
                    Size = manifest.GetProperty("size").GetInt64()
                };

                // Optional artifactType
                if (manifest.TryGetProperty("artifactType", out var artifactTypeProp))
                {
                    referrer.ArtifactType = artifactTypeProp.GetString();
                }

                // Optional annotations
                if (manifest.TryGetProperty("annotations", out var annotationsProp))
                {
                    referrer.Annotations = new Dictionary<string, string>();
                    foreach (var annotation in annotationsProp.EnumerateObject())
                    {
                        referrer.Annotations[annotation.Name] = annotation.Value.GetString() ?? "";
                    }
                }

                referrers.Add(referrer);
            }
        }

        // Filter by artifact type if specified
        if (!string.IsNullOrEmpty(artifactType))
        {
            referrers = referrers.Where(r => r.ArtifactType == artifactType).ToList();
        }

        return referrers;
    }

    public async Task UpdateReferrersIndexAsync(string name, string subjectDigest, ReferrerDescriptor referrer, CancellationToken cancellationToken = default)
    {
        // Check if referrers API is enabled
        var enableReferrersApi = bool.Parse(_configuration?["Registry:EnableReferrersApi"] ?? "false");
        if (!enableReferrersApi)
        {
            throw new InvalidOperationException("Referrers API is disabled. Enable it by setting Registry:EnableReferrersApi to true in configuration.");
        }

        // Validate repository name
        if (!NameValidator.IsValidRepositoryName(name))
        {
            throw new InvalidNameException(name, "Invalid repository name format");
        }

        // Validate digest format
        DigestValidator.ValidateDigest(subjectDigest);

        // Build storage key for referrers index
        var key = $"referrers/{name}/{subjectDigest}/index.json";

        // Read existing index or create new one
        var manifests = new List<object>();
        
        if (await _storage.ExistsAsync(key, cancellationToken))
        {
            var existingStream = await _storage.GetStreamAsync(key, cancellationToken);
            var existingDoc = await System.Text.Json.JsonDocument.ParseAsync(existingStream, cancellationToken: cancellationToken);
            
            if (existingDoc.RootElement.TryGetProperty("manifests", out var existingManifests))
            {
                foreach (var manifest in existingManifests.EnumerateArray())
                {
                    // Convert to anonymous object for serialization
                    var obj = new
                    {
                        mediaType = manifest.GetProperty("mediaType").GetString(),
                        digest = manifest.GetProperty("digest").GetString(),
                        size = manifest.GetProperty("size").GetInt64(),
                        artifactType = manifest.TryGetProperty("artifactType", out var at) ? at.GetString() : null,
                        annotations = manifest.TryGetProperty("annotations", out var ann) 
                            ? ann.EnumerateObject().ToDictionary(p => p.Name, p => p.Value.GetString()) 
                            : null
                    };
                    manifests.Add(obj!);
                }
            }
        }

        // Add new referrer
        var newReferrer = new
        {
            mediaType = referrer.MediaType,
            digest = referrer.Digest,
            size = referrer.Size,
            artifactType = referrer.ArtifactType,
            annotations = referrer.Annotations
        };
        manifests.Add(newReferrer);

        // Create updated index
        var index = new
        {
            schemaVersion = 2,
            mediaType = "application/vnd.oci.image.index.v1+json",
            manifests
        };

        // Serialize and store
        var indexJson = System.Text.Json.JsonSerializer.Serialize(index, new System.Text.Json.JsonSerializerOptions
        {
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
        });
        var indexBytes = System.Text.Encoding.UTF8.GetBytes(indexJson);

        await _storage.PutAsync(key, indexBytes, "application/vnd.oci.image.index.v1+json", cancellationToken);
    }

}

