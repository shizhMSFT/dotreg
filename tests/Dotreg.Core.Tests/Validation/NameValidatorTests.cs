using Dotreg.Core.Exceptions;
using Dotreg.Core.Validation;
using FluentAssertions;

namespace Dotreg.Core.Tests.Validation;

public class NameValidatorTests
{
    [Theory]
    [InlineData("myrepo")]
    [InlineData("my-repo")]
    [InlineData("my_repo")]
    [InlineData("my.repo")]
    [InlineData("myorg/myrepo")]
    [InlineData("myorg/sub/myrepo")]
    [InlineData("a1")]
    public void ValidateRepositoryName_ValidNames_ShouldReturnTrue(string name)
    {
        // Act
        var result = NameValidator.ValidateRepositoryName(name);

        // Assert
        result.Should().BeTrue();
    }

    [Theory]
    [InlineData("MyRepo", "lowercase")]
    [InlineData("my repo", "lowercase")]
    [InlineData("my@repo", "alphanumeric")]
    [InlineData("a", "between 2 and 255")]
    [InlineData("", "cannot be empty")]
    [InlineData(null, "cannot be empty")]
    public void ValidateRepositoryName_InvalidNames_ShouldThrowInvalidNameException(string name, string expectedReason)
    {
        // Act
        Action act = () => NameValidator.ValidateRepositoryName(name);

        // Assert
        act.Should().Throw<InvalidNameException>()
            .WithMessage($"*{expectedReason}*");
    }

    [Theory]
    [InlineData("latest")]
    [InlineData("v1.0.0")]
    [InlineData("main")]
    [InlineData("feature_branch")]
    [InlineData("1.2.3-alpha")]
    [InlineData("_test")]
    public void ValidateTagName_ValidTags_ShouldReturnTrue(string tag)
    {
        // Act
        var result = NameValidator.ValidateTagName(tag);

        // Assert
        result.Should().BeTrue();
    }

    [Theory]
    [InlineData("", "cannot be empty")]
    [InlineData(null, "cannot be empty")]
    [InlineData("tag with space", "alphanumeric")]
    [InlineData("tag@invalid", "alphanumeric")]
    public void ValidateTagName_InvalidTags_ShouldThrowInvalidNameException(string tag, string expectedReason)
    {
        // Act
        Action act = () => NameValidator.ValidateTagName(tag);

        // Assert
        act.Should().Throw<InvalidNameException>()
            .WithMessage($"*{expectedReason}*");
    }

    [Fact]
    public void ValidateRepositoryName_TooLongName_ShouldThrowInvalidNameException()
    {
        // Arrange
        var name = new string('a', 256);

        // Act
        Action act = () => NameValidator.ValidateRepositoryName(name);

        // Assert
        act.Should().Throw<InvalidNameException>()
            .WithMessage("*between 2 and 255*");
    }

    [Fact]
    public void ValidateTagName_TooLongTag_ShouldThrowInvalidNameException()
    {
        // Arrange
        var tag = new string('a', 129);

        // Act
        Action act = () => NameValidator.ValidateTagName(tag);

        // Assert
        act.Should().Throw<InvalidNameException>()
            .WithMessage("*between 1 and 128*");
    }
}
