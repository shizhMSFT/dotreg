using Amazon.S3;
using Amazon.S3.Model;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using Dotreg.Storage.S3;
using FluentAssertions;
using Xunit;

namespace Dotreg.Storage.S3.Tests;

/// <summary>
/// Shared fixture for LocalStack container - started once and reused across all tests
/// </summary>
public class LocalStackFixture : IAsyncLifetime
{
    private IContainer? _localStackContainer;
    public IAmazonS3? S3Client { get; private set; }
    public S3Config? Config { get; private set; }
    private const string BucketName = "test-registry";

    public async Task InitializeAsync()
    {
        // Start LocalStack container once for all tests
        _localStackContainer = new ContainerBuilder()
            .WithImage("localstack/localstack:latest")
            .WithPortBinding(4566, true)  // Let Docker assign random host port
            .WithEnvironment("SERVICES", "s3")
            .WithEnvironment("DEBUG", "1")
            .WithWaitStrategy(Wait.ForUnixContainer().UntilHttpRequestIsSucceeded(r => r.ForPort(4566)))
            .Build();

        await _localStackContainer.StartAsync();

        // Get the dynamically assigned host port
        var hostPort = _localStackContainer.GetMappedPublicPort(4566);

        // Configure S3 client for LocalStack with dynamic port
        Config = new S3Config
        {
            ServiceUrl = $"http://localhost:{hostPort}",
            AccessKeyId = "test",
            SecretAccessKey = "test",
            BucketName = BucketName,
            UsePathStyle = true
        };

        var s3Config = new Amazon.S3.AmazonS3Config
        {
            ServiceURL = Config.ServiceUrl,
            ForcePathStyle = Config.UsePathStyle
        };

        S3Client = new AmazonS3Client(Config.AccessKeyId, Config.SecretAccessKey, s3Config);

        // Create test bucket
        await S3Client.PutBucketAsync(BucketName);
    }

    public async Task DisposeAsync()
    {
        // Clean up at end of all tests
        if (S3Client != null)
        {
            try
            {
                // Delete all objects in bucket
                var listResponse = await S3Client.ListObjectsV2Async(new ListObjectsV2Request { BucketName = BucketName });
                if (listResponse.S3Objects.Count > 0)
                {
                    var deleteRequest = new DeleteObjectsRequest
                    {
                        BucketName = BucketName,
                        Objects = listResponse.S3Objects.Select(o => new KeyVersion { Key = o.Key }).ToList()
                    };
                    await S3Client.DeleteObjectsAsync(deleteRequest);
                }

                // Delete bucket
                await S3Client.DeleteBucketAsync(BucketName);
            }
            catch
            {
                // Ignore cleanup errors
            }

            S3Client.Dispose();
        }

        if (_localStackContainer != null)
        {
            await _localStackContainer.StopAsync();
            await _localStackContainer.DisposeAsync();
        }
    }

    public async Task CleanupTestDataAsync()
    {
        // Clean up test data between tests to ensure isolation
        if (S3Client != null)
        {
            try
            {
                var listResponse = await S3Client.ListObjectsV2Async(new ListObjectsV2Request { BucketName = BucketName });
                if (listResponse.S3Objects.Count > 0)
                {
                    var deleteRequest = new DeleteObjectsRequest
                    {
                        BucketName = BucketName,
                        Objects = listResponse.S3Objects.Select(o => new KeyVersion { Key = o.Key }).ToList()
                    };
                    await S3Client.DeleteObjectsAsync(deleteRequest);
                }
            }
            catch
            {
                // Ignore cleanup errors
            }
        }
    }
}

/// <summary>
/// Integration tests for S3StorageProvider using LocalStack (Testcontainers)
/// Tests actual S3 operations for manifest and blob storage/retrieval
/// </summary>
public class S3StorageProviderTests : IClassFixture<LocalStackFixture>, IAsyncLifetime
{
    private readonly LocalStackFixture _fixture;
    private S3StorageProvider? _provider;

    public S3StorageProviderTests(LocalStackFixture fixture)
    {
        _fixture = fixture;
    }

    public Task InitializeAsync()
    {
        // Initialize provider for each test
        _provider = new S3StorageProvider(_fixture.S3Client!, _fixture.Config!);
        return Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        // Clean up test data after each test to ensure isolation
        await _fixture.CleanupTestDataAsync();
    }

    [Fact]
    public async Task GetManifestAsync_WithExistingManifest_ShouldReturnManifestData()
    {
        // Arrange
        var name = "library/nginx";
        var reference = "latest";
        var manifestContent = """{"schemaVersion": 2, "mediaType": "application/vnd.oci.image.manifest.v1+json"}""";
        var key = $"manifests/{name}/{reference}";
        
        // Upload test manifest to S3
        await _provider!.PutAsync(key, System.Text.Encoding.UTF8.GetBytes(manifestContent), "application/vnd.oci.image.manifest.v1+json");

        // Act
        var data = await _provider.GetAsync(key);

        // Assert
        data.Should().NotBeNull();
        System.Text.Encoding.UTF8.GetString(data).Should().Contain("schemaVersion");
    }

    [Fact]
    public async Task GetManifestAsync_WithNonExistentManifest_ShouldThrowException()
    {
        // Arrange
        var name = "library/nginx";
        var reference = "nonexistent";

        // Act
        var act = async () => await _provider!.GetAsync($"manifests/{name}/{reference}");

        // Assert
        await act.Should().ThrowAsync<Exception>();
    }

    [Fact]
    public async Task GetBlobStreamAsync_WithExistingBlob_ShouldReturnStream()
    {
        // Arrange
        var name = "library/nginx";
        var digest = "sha256:abcdef1234567890abcdef1234567890abcdef1234567890abcdef1234567890";
        var blobContent = new byte[] { 1, 2, 3, 4, 5 };
        var key = $"blobs/{name}/{digest}";
        
        // Upload test blob to S3
        await _provider!.PutStreamAsync(key, new MemoryStream(blobContent), "application/octet-stream");

        // Act
        var stream = await _provider.GetStreamAsync(key);

        // Assert
        stream.Should().NotBeNull();
        stream.CanRead.Should().BeTrue();
        var buffer = new byte[blobContent.Length];
        var bytesRead = await stream.ReadAsync(buffer);
        bytesRead.Should().Be(blobContent.Length);
        buffer.Should().Equal(blobContent);
    }

    [Fact]
    public async Task GetBlobStreamAsync_WithNonExistentBlob_ShouldThrowException()
    {
        // Arrange
        var name = "library/nginx";
        var digest = "sha256:nonexistent0000000000000000000000000000000000000000000000000000";

        // Act
        var act = async () => await _provider!.GetStreamAsync($"blobs/{name}/{digest}");

        // Assert
        await act.Should().ThrowAsync<Exception>();
    }

    [Fact]
    public async Task PutManifestAsync_ShouldStoreManifestInS3()
    {
        // Arrange
        var name = "library/nginx";
        var reference = "latest";
        var manifestContent = """{"schemaVersion": 2, "mediaType": "application/vnd.oci.image.manifest.v1+json"}"""u8.ToArray();

        // Act
        await _provider!.PutAsync($"manifests/{name}/{reference}", manifestContent, "application/vnd.oci.image.manifest.v1+json");

        // Assert
        var exists = await _provider.ExistsAsync($"manifests/{name}/{reference}");
        exists.Should().BeTrue();
    }

    [Fact]
    public async Task PutBlobStreamAsync_ShouldStoreBlobInS3()
    {
        // Arrange
        var name = "library/nginx";
        var digest = "sha256:abcdef1234567890abcdef1234567890abcdef1234567890abcdef1234567890";
        var blobContent = new byte[] { 1, 2, 3, 4, 5 };
        var stream = new MemoryStream(blobContent);

        // Act
        await _provider!.PutStreamAsync($"blobs/{name}/{digest}", stream, "application/octet-stream");

        // Assert
        var exists = await _provider.ExistsAsync($"blobs/{name}/{digest}");
        exists.Should().BeTrue();
    }

    [Fact]
    public async Task ExistsAsync_WithExistingObject_ShouldReturnTrue()
    {
        // Arrange
        var key = "test/exists";
        await _provider!.PutAsync(key, "test data"u8.ToArray(), "text/plain");

        // Act
        var exists = await _provider.ExistsAsync(key);

        // Assert
        exists.Should().BeTrue();
    }

    [Fact]
    public async Task ExistsAsync_WithNonExistentObject_ShouldReturnFalse()
    {
        // Arrange
        var key = "test/nonexistent";

        // Act
        var exists = await _provider!.ExistsAsync(key);

        // Assert
        exists.Should().BeFalse();
    }

    [Fact]
    public async Task GetSizeAsync_ShouldReturnCorrectSize()
    {
        // Arrange
        var key = "test/size";
        var content = new byte[12345];
        await _provider!.PutAsync(key, content, "application/octet-stream");

        // Act
        var size = await _provider.GetSizeAsync(key);

        // Assert
        size.Should().Be(12345);
    }

    [Fact]
    public async Task GetMetadataAsync_ShouldReturnStoredMetadata()
    {
        // Arrange
        var key = "test/metadata";
        var metadata = new Dictionary<string, string>
        {
            ["digest"] = "sha256:abc123",
            ["mediaType"] = "application/vnd.oci.image.manifest.v1+json"
        };
        await _provider!.PutWithMetadataAsync(key, "test"u8.ToArray(), "text/plain", metadata);

        // Act
        var retrievedMetadata = await _provider.GetMetadataAsync(key);

        // Assert
        retrievedMetadata.Should().ContainKey("digest");
        retrievedMetadata["digest"].Should().Be("sha256:abc123");
    }
}
