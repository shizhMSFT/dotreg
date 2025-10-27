using Amazon.S3;
using Amazon.S3.Model;
using Dotreg.Core.Services;
using Dotreg.Storage.S3.Exceptions;

namespace Dotreg.Storage.S3;

/// <summary>
/// S3-based implementation of storage service.
/// </summary>
public class S3StorageProvider : IStorageService
{
    private readonly IAmazonS3 _s3Client;
    private readonly S3Config _config;

    public S3StorageProvider(IAmazonS3 s3Client, S3Config config)
    {
        _s3Client = s3Client ?? throw new ArgumentNullException(nameof(s3Client));
        _config = config ?? throw new ArgumentNullException(nameof(config));
    }

    public async Task<bool> ExistsAsync(string key, CancellationToken cancellationToken = default)
    {
        try
        {
            await _s3Client.GetObjectMetadataAsync(new GetObjectMetadataRequest
            {
                BucketName = _config.BucketName,
                Key = key
            }, cancellationToken);
            return true;
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return false;
        }
    }

    public async Task<long> GetSizeAsync(string key, CancellationToken cancellationToken = default)
    {
        try
        {
            var metadata = await _s3Client.GetObjectMetadataAsync(new GetObjectMetadataRequest
            {
                BucketName = _config.BucketName,
                Key = key
            }, cancellationToken);
            return metadata.ContentLength;
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            throw new S3StorageException("GetSize", key, ex);
        }
        catch (AmazonS3Exception ex)
        {
            throw new S3StorageException("GetSize", key, ex);
        }
    }

    public async Task<string?> GetContentTypeAsync(string key, CancellationToken cancellationToken = default)
    {
        try
        {
            var metadata = await _s3Client.GetObjectMetadataAsync(new GetObjectMetadataRequest
            {
                BucketName = _config.BucketName,
                Key = key
            }, cancellationToken);
            return metadata.Headers.ContentType;
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            throw new S3StorageException("GetContentType", key, ex);
        }
        catch (AmazonS3Exception ex)
        {
            throw new S3StorageException("GetContentType", key, ex);
        }
    }

    public async Task<byte[]> GetAsync(string key, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _s3Client.GetObjectAsync(new GetObjectRequest
            {
                BucketName = _config.BucketName,
                Key = key
            }, cancellationToken);

            using var memoryStream = new MemoryStream();
            await response.ResponseStream.CopyToAsync(memoryStream, cancellationToken);
            return memoryStream.ToArray();
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            throw new S3StorageException("GetAsync", key, ex);
        }
        catch (AmazonS3Exception ex)
        {
            throw new S3StorageException("GetAsync", key, ex);
        }
    }

    public async Task<Stream> GetStreamAsync(string key, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _s3Client.GetObjectAsync(new GetObjectRequest
            {
                BucketName = _config.BucketName,
                Key = key
            }, cancellationToken);

            // Create a memory stream to return (response stream will be disposed when GetObjectResponse is disposed)
            var memoryStream = new MemoryStream();
            await response.ResponseStream.CopyToAsync(memoryStream, cancellationToken);
            memoryStream.Position = 0;
            return memoryStream;
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            throw new S3StorageException("GetStreamAsync", key, ex);
        }
        catch (AmazonS3Exception ex)
        {
            throw new S3StorageException("GetStreamAsync", key, ex);
        }
    }

    public async Task PutAsync(string key, byte[] content, string? contentType = null, CancellationToken cancellationToken = default)
    {
        try
        {
            using var stream = new MemoryStream(content);
            var request = new PutObjectRequest
            {
                BucketName = _config.BucketName,
                Key = key,
                InputStream = stream,
                ContentType = contentType ?? "application/octet-stream"
            };

            await _s3Client.PutObjectAsync(request, cancellationToken);
        }
        catch (AmazonS3Exception ex)
        {
            throw new S3StorageException("PutAsync", key, ex);
        }
    }

    public async Task PutStreamAsync(string key, Stream content, string? contentType = null, CancellationToken cancellationToken = default)
    {
        try
        {
            var request = new PutObjectRequest
            {
                BucketName = _config.BucketName,
                Key = key,
                InputStream = content,
                ContentType = contentType ?? "application/octet-stream"
            };

            await _s3Client.PutObjectAsync(request, cancellationToken);
        }
        catch (AmazonS3Exception ex)
        {
            throw new S3StorageException("PutStreamAsync", key, ex);
        }
    }

    public async Task DeleteAsync(string key, CancellationToken cancellationToken = default)
    {
        try
        {
            await _s3Client.DeleteObjectAsync(new DeleteObjectRequest
            {
                BucketName = _config.BucketName,
                Key = key
            }, cancellationToken);
        }
        catch (AmazonS3Exception ex)
        {
            throw new S3StorageException("DeleteAsync", key, ex);
        }
    }

    public async Task<List<string>> ListAsync(string prefix, int? maxResults = null, string? startAfter = null, CancellationToken cancellationToken = default)
    {
        try
        {
            var request = new ListObjectsV2Request
            {
                BucketName = _config.BucketName,
                Prefix = prefix,
                MaxKeys = maxResults ?? 1000
            };

            if (!string.IsNullOrEmpty(startAfter))
            {
                request.StartAfter = startAfter;
            }

            var response = await _s3Client.ListObjectsV2Async(request, cancellationToken);
            return response.S3Objects.Select(o => o.Key).ToList();
        }
        catch (AmazonS3Exception ex)
        {
            throw new S3StorageException("ListAsync", prefix, ex);
        }
    }

    public async Task<Dictionary<string, string>> GetMetadataAsync(string key, CancellationToken cancellationToken = default)
    {
        try
        {
            var metadata = await _s3Client.GetObjectMetadataAsync(new GetObjectMetadataRequest
            {
                BucketName = _config.BucketName,
                Key = key
            }, cancellationToken);

            // S3 returns metadata keys with 'x-amz-meta-' prefix, strip it for consistency
            return metadata.Metadata.Keys.Cast<string>()
                .ToDictionary(
                    k => k.StartsWith("x-amz-meta-", StringComparison.OrdinalIgnoreCase) 
                        ? k["x-amz-meta-".Length..] 
                        : k,
                    k => metadata.Metadata[k]);
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            throw new S3StorageException("GetMetadataAsync", key, ex);
        }
        catch (AmazonS3Exception ex)
        {
            throw new S3StorageException("GetMetadataAsync", key, ex);
        }
    }

    public async Task PutWithMetadataAsync(string key, byte[] content, string? contentType, Dictionary<string, string>? metadata, CancellationToken cancellationToken = default)
    {
        try
        {
            using var stream = new MemoryStream(content);
            var request = new PutObjectRequest
            {
                BucketName = _config.BucketName,
                Key = key,
                InputStream = stream,
                ContentType = contentType ?? "application/octet-stream"
            };

            if (metadata != null)
            {
                foreach (var kvp in metadata)
                {
                    request.Metadata.Add(kvp.Key, kvp.Value);
                }
            }

            await _s3Client.PutObjectAsync(request, cancellationToken);
        }
        catch (AmazonS3Exception ex)
        {
            throw new S3StorageException("PutWithMetadataAsync", key, ex);
        }
    }

    public async Task StoreMetadataAsync(string repository, string sessionKey, Dictionary<string, string> metadata, CancellationToken cancellationToken = default)
    {
        try
        {
            var key = S3KeyBuilder.BuildUploadSessionKey(repository, sessionKey);
            var content = System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(metadata);

            await _s3Client.PutObjectAsync(new PutObjectRequest
            {
                BucketName = _config.BucketName,
                Key = key,
                InputStream = new MemoryStream(content),
                ContentType = "application/json"
            }, cancellationToken);
        }
        catch (AmazonS3Exception ex)
        {
            throw new S3StorageException("StoreMetadataAsync", sessionKey, ex);
        }
    }

    public async Task AppendToUploadAsync(string repository, string sessionKey, Stream content, long startByte, long length, CancellationToken cancellationToken = default)
    {
        try
        {
            var key = S3KeyBuilder.BuildUploadDataKey(repository, sessionKey);
            
            // For simplicity, we'll store each chunk as a separate part
            // In production, consider using S3 multipart uploads
            var partKey = $"{key}/part-{startByte}";
            
            await _s3Client.PutObjectAsync(new PutObjectRequest
            {
                BucketName = _config.BucketName,
                Key = partKey,
                InputStream = content,
                ContentType = "application/octet-stream"
            }, cancellationToken);
        }
        catch (AmazonS3Exception ex)
        {
            throw new S3StorageException("AppendToUploadAsync", sessionKey, ex);
        }
    }

    public async Task<Stream> GetUploadContentAsync(string repository, string sessionKey, CancellationToken cancellationToken = default)
    {
        try
        {
            var key = S3KeyBuilder.BuildUploadDataKey(repository, sessionKey);
            
            // List all parts and combine them
            var listResponse = await _s3Client.ListObjectsV2Async(new ListObjectsV2Request
            {
                BucketName = _config.BucketName,
                Prefix = $"{key}/part-"
            }, cancellationToken);

            if (listResponse.S3Objects.Count == 0)
            {
                return new MemoryStream();
            }

            // Combine all parts into a single stream
            var combinedStream = new MemoryStream();
            foreach (var obj in listResponse.S3Objects.OrderBy(o => o.Key))
            {
                var response = await _s3Client.GetObjectAsync(new GetObjectRequest
                {
                    BucketName = _config.BucketName,
                    Key = obj.Key
                }, cancellationToken);

                await response.ResponseStream.CopyToAsync(combinedStream, cancellationToken);
            }

            combinedStream.Position = 0;
            return combinedStream;
        }
        catch (AmazonS3Exception ex)
        {
            throw new S3StorageException("GetUploadContentAsync", sessionKey, ex);
        }
    }

    public async Task StoreBlobAsync(string repository, string digest, Stream content, Dictionary<string, string>? metadata, CancellationToken cancellationToken = default)
    {
        try
        {
            var key = S3KeyBuilder.BuildBlobKey(repository, digest);
            var request = new PutObjectRequest
            {
                BucketName = _config.BucketName,
                Key = key,
                InputStream = content,
                ContentType = "application/octet-stream"
            };

            if (metadata != null)
            {
                foreach (var kvp in metadata)
                {
                    request.Metadata.Add(kvp.Key, kvp.Value);
                }
            }

            await _s3Client.PutObjectAsync(request, cancellationToken);
        }
        catch (AmazonS3Exception ex)
        {
            throw new S3StorageException("StoreBlobAsync", digest, ex);
        }
    }

    public async Task DeleteUploadSessionAsync(string repository, string sessionKey, CancellationToken cancellationToken = default)
    {
        try
        {
            // Delete session metadata
            var metadataKey = S3KeyBuilder.BuildUploadSessionKey(repository, sessionKey);
            await _s3Client.DeleteObjectAsync(new DeleteObjectRequest
            {
                BucketName = _config.BucketName,
                Key = metadataKey
            }, cancellationToken);

            // Delete all upload parts
            var dataKey = S3KeyBuilder.BuildUploadDataKey(repository, sessionKey);
            var listResponse = await _s3Client.ListObjectsV2Async(new ListObjectsV2Request
            {
                BucketName = _config.BucketName,
                Prefix = $"{dataKey}/part-"
            }, cancellationToken);

            foreach (var obj in listResponse.S3Objects)
            {
                await _s3Client.DeleteObjectAsync(new DeleteObjectRequest
                {
                    BucketName = _config.BucketName,
                    Key = obj.Key
                }, cancellationToken);
            }
        }
        catch (AmazonS3Exception ex)
        {
            throw new S3StorageException("DeleteUploadSessionAsync", sessionKey, ex);
        }
    }
}
