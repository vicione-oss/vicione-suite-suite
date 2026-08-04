using Core.Shared.UserManagement.Contracts;
using Core.Shared.UserManagement.Extensions;
using Core.Shared.UserManagement.Mappers;
using Sdk.Authorization;
using Sdk.UserManagement.Contracts;

namespace Core.Shared.Tests.UserManagement.Extensions;

public sealed class UserManagementClaimExtensionsTests
{
    private const string ModuleId = "MyModule";

    private readonly UserProfileMapper _userProfileMapper = new();
    private readonly string _adminRole = "Admin";
    private readonly string _userRole = "User";
    private readonly UserManagementClaim _adminAccessLevelClaim = ModuleAuthorizationClaimFactory.CreateClaim(ModuleId, AccessLevel.Full, ModuleId).ToUserManagementClaim();
    private readonly UserManagementClaim _userAccessLevelClaim = ModuleAuthorizationClaimFactory.CreateClaim(ModuleId, AccessLevel.Partial, ModuleId).ToUserManagementClaim();

    [Fact]
    public void Should_detect_authorization_change_when_role_was_added()
    {
        // Arrange
        var userProfile = CreateUserProfile();
        userProfile.Roles.Add(_adminRole);

        var userProfileBefore = _userProfileMapper.Map(userProfile);

        userProfile.Roles.Add(_userRole);

        // Act
        var result = userProfile.IsAuthorizationChanged(userProfileBefore);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void Should_detect_authorization_change_when_role_was_removed()
    {
        // Arrange
        var userProfile = CreateUserProfile();
        userProfile.Roles.Add(_adminRole);
        userProfile.Roles.Add(_userRole);

        var userProfileBefore = _userProfileMapper.Map(userProfile);

        userProfile.Roles.Remove(_userRole);

        // Act
        var result = userProfile.IsAuthorizationChanged(userProfileBefore);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void Should_detect_authorization_change_when_module_authorization_claim_was_added()
    {
        // Arrange
        var userProfile = CreateUserProfile();
        userProfile.Claims.Add(_adminAccessLevelClaim);

        var userProfileBefore = _userProfileMapper.Map(userProfile);

        userProfile.Claims.Add(_userAccessLevelClaim);

        // Act
        var result = userProfile.IsAuthorizationChanged(userProfileBefore);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void Should_detect_authorization_change_when_module_authorization_claim_was_removed()
    {
        // Arrange
        var userProfile = CreateUserProfile();
        userProfile.Claims.Add(_adminAccessLevelClaim);
        userProfile.Claims.Add(_userAccessLevelClaim);

        var userProfileBefore = _userProfileMapper.Map(userProfile);

        userProfile.Claims.Remove(_userAccessLevelClaim);

        // Act
        var result = userProfile.IsAuthorizationChanged(userProfileBefore);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void Should_not_detect_authorization_change_when_neither_roles_nor_module_authorization_claims_were_changed()
    {
        // Arrange
        var userProfile = CreateUserProfile();
        userProfile.Claims.Add(_adminAccessLevelClaim);
        userProfile.Claims.Add(_userAccessLevelClaim);

        var userProfileBefore = _userProfileMapper.Map(userProfile);

        // Act
        var result = userProfile.IsAuthorizationChanged(userProfileBefore);

        // Assert
        result.Should().BeFalse();
    }

    private static UserProfile CreateUserProfile()
        => new() { UserName = new UserName("Waldo") };
}
