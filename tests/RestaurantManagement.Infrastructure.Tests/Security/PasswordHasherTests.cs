using FluentAssertions;
using RestaurantManagement.Infrastructure.Security;
using Xunit;

namespace RestaurantManagement.Infrastructure.Tests.Security;

public class PasswordHasherTests
{
    private readonly PasswordHasher _hasher = new();

    [Theory]
    [InlineData("Admin123!")]
    [InlineData("SuperSecurePassword999")]
    [InlineData("simple_pwd")]
    [InlineData(" ")]
    public void HashPassword_ShouldReturnValidHash_AndVerifySuccessfully(string password)
    {
        // Act
        var hash = _hasher.HashPassword(password);

        // Assert
        hash.Should().NotBeNullOrWhiteSpace();
        hash.Should().StartWith("pbkdf2$100000$");

        var isMatch = _hasher.VerifyPassword(password, hash);
        isMatch.Should().BeTrue();
    }

    [Fact]
    public void VerifyPassword_ShouldReturnFalse_WhenPasswordDoesNotMatch()
    {
        // Arrange
        var hash = _hasher.HashPassword("CorrectPassword");

        // Act
        var isMatch = _hasher.VerifyPassword("WrongPassword", hash);

        // Assert
        isMatch.Should().BeFalse();
    }

    [Fact]
    public void HashPassword_ShouldGenerateUniqueHashesForSamePassword_DueToUniqueSalts()
    {
        // Act
        var hash1 = _hasher.HashPassword("SamePassword");
        var hash2 = _hasher.HashPassword("SamePassword");

        // Assert
        hash1.Should().NotBe(hash2);
        _hasher.VerifyPassword("SamePassword", hash1).Should().BeTrue();
        _hasher.VerifyPassword("SamePassword", hash2).Should().BeTrue();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("invalid-format")]
    [InlineData("pbkdf2$invalid$salt$hash")]
    public void VerifyPassword_ShouldReturnFalse_WhenHashIsMalformed(string? malformedHash)
    {
        // Act
        var isMatch = _hasher.VerifyPassword("password", malformedHash!);

        // Assert
        isMatch.Should().BeFalse();
    }
}
