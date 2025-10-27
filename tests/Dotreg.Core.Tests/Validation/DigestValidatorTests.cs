using System.Text;
using Dotreg.Core.Exceptions;
using Dotreg.Core.Validation;
using FluentAssertions;

namespace Dotreg.Core.Tests.Validation;

public class DigestValidatorTests
{
    [Theory]
    [InlineData("sha256:a3ed95caeb02ffe68cdd9fd84406680ae93d633cb16422d00e8a7c22955b46d4")]
    [InlineData("sha256:0000000000000000000000000000000000000000000000000000000000000000")]
    [InlineData("sha512:abcdef1234567890abcdef1234567890abcdef1234567890abcdef1234567890abcdef1234567890abcdef1234567890abcdef1234567890abcdef1234567890")]
    public void ValidateDigest_ValidDigests_ShouldReturnTrue(string digest)
    {
        // Act
        var result = DigestValidator.ValidateDigest(digest);

        // Assert
        result.Should().BeTrue();
    }

    [Theory]
    [InlineData("invalid")]
    [InlineData("sha256:")]
    [InlineData("sha256:xyz")]
    [InlineData(":a3ed95caeb02ffe68cdd9fd84406680ae93d633cb16422d00e8a7c22955b46d4")]
    [InlineData("")]
    [InlineData(null)]
    public void ValidateDigest_InvalidDigests_ShouldThrowArgumentException(string digest)
    {
        // Act
        Action act = () => DigestValidator.ValidateDigest(digest);

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void CalculateSha256_ValidContent_ShouldReturnCorrectDigest()
    {
        // Arrange
        var content = Encoding.UTF8.GetBytes("hello world");

        // Act
        var digest = DigestValidator.CalculateSha256(content);

        // Assert
        digest.Should().Be("sha256:b94d27b9934d3e08a52e52d7da7dabfac484efe37a5380ee9088f7ace2efcde9");
    }

    [Fact]
    public async Task CalculateSha256Async_ValidStream_ShouldReturnCorrectDigest()
    {
        // Arrange
        var content = Encoding.UTF8.GetBytes("hello world");
        using var stream = new MemoryStream(content);

        // Act
        var digest = await DigestValidator.CalculateSha256Async(stream);

        // Assert
        digest.Should().Be("sha256:b94d27b9934d3e08a52e52d7da7dabfac484efe37a5380ee9088f7ace2efcde9");
    }

    [Fact]
    public void VerifyDigest_MatchingDigest_ShouldNotThrow()
    {
        // Arrange
        var content = Encoding.UTF8.GetBytes("hello world");
        var expectedDigest = "sha256:b94d27b9934d3e08a52e52d7da7dabfac484efe37a5380ee9088f7ace2efcde9";

        // Act
        Action act = () => DigestValidator.VerifyDigest(content, expectedDigest);

        // Assert
        act.Should().NotThrow();
    }

    [Fact]
    public void VerifyDigest_MismatchedDigest_ShouldThrowDigestMismatchException()
    {
        // Arrange
        var content = Encoding.UTF8.GetBytes("hello world");
        var wrongDigest = "sha256:0000000000000000000000000000000000000000000000000000000000000000";

        // Act
        Action act = () => DigestValidator.VerifyDigest(content, wrongDigest);

        // Assert
        act.Should().Throw<DigestMismatchException>()
            .WithMessage("*expected*got*");
    }

    [Fact]
    public async Task VerifyDigestAsync_MatchingDigest_ShouldNotThrow()
    {
        // Arrange
        var content = Encoding.UTF8.GetBytes("hello world");
        using var stream = new MemoryStream(content);
        var expectedDigest = "sha256:b94d27b9934d3e08a52e52d7da7dabfac484efe37a5380ee9088f7ace2efcde9";

        // Act
        Func<Task> act = async () => await DigestValidator.VerifyDigestAsync(stream, expectedDigest);

        // Assert
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task VerifyDigestAsync_MismatchedDigest_ShouldThrowDigestMismatchException()
    {
        // Arrange
        var content = Encoding.UTF8.GetBytes("hello world");
        using var stream = new MemoryStream(content);
        var wrongDigest = "sha256:0000000000000000000000000000000000000000000000000000000000000000";

        // Act
        Func<Task> act = async () => await DigestValidator.VerifyDigestAsync(stream, wrongDigest);

        // Assert
        await act.Should().ThrowAsync<DigestMismatchException>()
            .WithMessage("*expected*got*");
    }

    [Fact]
    public void CalculateSha256_EmptyContent_ShouldReturnEmptyDigest()
    {
        // Arrange
        var content = Array.Empty<byte>();

        // Act
        var digest = DigestValidator.CalculateSha256(content);

        // Assert
        digest.Should().Be("sha256:e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855");
    }
}
