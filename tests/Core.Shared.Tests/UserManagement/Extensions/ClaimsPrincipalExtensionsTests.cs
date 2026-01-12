using System.Security.Claims;
using Core.Shared.UserManagement.Contracts;
using Core.Shared.UserManagement.Extensions;
using AwesomeAssertions;
using Xunit;

namespace Core.Shared.Tests.UserManagement.Extensions;

public sealed class ClaimsPrincipalExtensionsTests
{
    [Fact]
    public void Should_detect_association_with_user_profile()
    {
        // Arrange
        var user = CreateClaimsPrincipal("Waldo");
        var userProfile = CreateUserProfile("Waldo");

        // Act
        var result = user.IsAssociatedWith(userProfile);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void Should_not_detect_association_with_user_profile()
    {
        // Arrange
        var user = CreateClaimsPrincipal("Garply");
        var userProfile = CreateUserProfile("Waldo");

        // Act
        var result = user.IsAssociatedWith(userProfile);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void Should_not_detect_association_with_user_profile_when_user_is_null()
    {
        // Arrange
        ClaimsPrincipal? user = null;
        var userProfile = CreateUserProfile("Waldo");

        // Act
        var result = user.IsAssociatedWith(userProfile);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void Should_not_detect_association_with_user_profile_when_user_has_no_identity()
    {
        // Arrange
        var user = new ClaimsPrincipal();
        var userProfile = CreateUserProfile("Waldo");

        // Act
        var result = user.IsAssociatedWith(userProfile);

        // Assert
        result.Should().BeFalse();
    }

    private static ClaimsPrincipal CreateClaimsPrincipal(string userName)
    {
        var claimsIdentity = new ClaimsIdentity([new(ClaimTypes.Name, userName)]);

        return new(claimsIdentity);
    }

    private static UserProfile CreateUserProfile(string userName)
        => new() { UserName = new UserName(userName) };
}
