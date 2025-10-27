using Dotreg.Storage.S3;
using FluentAssertions;

namespace Dotreg.Storage.S3.Tests;

public class S3KeyBuilderTests
{
    [Fact]
    public void BuildManifestKey_ValidInputs_ShouldReturnCorrectKey()
    {
        // Arrange
        var repo = "myorg/myrepo";
        var digest = "sha256:abc123";

        // Act
        var key = S3KeyBuilder.BuildManifestKey(repo, digest);

        // Assert
        key.Should().Be("repositories/myorg/myrepo/manifests/sha256:abc123");
    }

    [Fact]
    public void BuildBlobKey_ValidInputs_ShouldReturnCorrectKey()
    {
        // Arrange
        var repo = "myorg/myrepo";
        var digest = "sha256:def456";

        // Act
        var key = S3KeyBuilder.BuildBlobKey(repo, digest);

        // Assert
        key.Should().Be("repositories/myorg/myrepo/blobs/sha256:def456");
    }

    [Fact]
    public void BuildTagKey_ValidInputs_ShouldReturnCorrectKey()
    {
        // Arrange
        var repo = "myorg/myrepo";
        var tag = "latest";

        // Act
        var key = S3KeyBuilder.BuildTagKey(repo, tag);

        // Assert
        key.Should().Be("repositories/myorg/myrepo/tags/latest");
    }

    [Fact]
    public void BuildUploadKey_ValidInputs_ShouldReturnCorrectKey()
    {
        // Arrange
        var repo = "myorg/myrepo";
        var uploadId = "uuid-1234";

        // Act
        var key = S3KeyBuilder.BuildUploadKey(repo, uploadId);

        // Assert
        key.Should().Be("repositories/myorg/myrepo/uploads/uuid-1234");
    }

    [Fact]
    public void BuildReferrersIndexKey_ValidInputs_ShouldReturnCorrectKey()
    {
        // Arrange
        var repo = "myorg/myrepo";
        var digest = "sha256:abc123";

        // Act
        var key = S3KeyBuilder.BuildReferrersIndexKey(repo, digest);

        // Assert
        key.Should().Be("repositories/myorg/myrepo/referrers/sha256:abc123/index.json");
    }

    [Fact]
    public void BuildTagsPrefix_ValidInput_ShouldReturnCorrectPrefix()
    {
        // Arrange
        var repo = "myorg/myrepo";

        // Act
        var prefix = S3KeyBuilder.BuildTagsPrefix(repo);

        // Assert
        prefix.Should().Be("repositories/myorg/myrepo/tags/");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void BuildManifestKey_InvalidRepositoryName_ShouldThrowArgumentException(string repo)
    {
        // Act
        Action act = () => S3KeyBuilder.BuildManifestKey(repo, "sha256:abc");

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void BuildManifestKey_InvalidDigest_ShouldThrowArgumentException(string digest)
    {
        // Act
        Action act = () => S3KeyBuilder.BuildManifestKey("repo", digest);

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void BuildTagKey_NestedRepository_ShouldHandleSlashes()
    {
        // Arrange
        var repo = "org/team/project";
        var tag = "v1.0.0";

        // Act
        var key = S3KeyBuilder.BuildTagKey(repo, tag);

        // Assert
        key.Should().Be("repositories/org/team/project/tags/v1.0.0");
    }
}
