using System.Security.Claims;
using Blazor.Server.Backend.Services;
using Blazor.Shared.UserManagement.Services.Validators;
using Core.Shared.UserManagement;
using Core.Shared.UserManagement.Contracts;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;
using Xunit;

namespace Blazor.Server.Tests.Services;

public class ExternalLoginServiceTest
{
    private const string LoginProvider = "OpenIdConnect";
    private const string ProviderKey = "some-sub-id";

    private static readonly SuiteUser ExistingUser = new()
    {
        UserName = "existing.user",
        Email = "existing@example.com"
    };

    [Fact]
    public async Task Should_return_null_when_email_claim_is_missing()
    {
        // Arrange
        var info = CreateExternalLoginInfo(
            new Claim("preferred_username", "john.doe"));
        var (sut, _) = CreateSubjectUnderTest(usernameValidatorResult: true, createAsyncResult: null, ExistingUser);

        // Act
        var result = await sut.CreateNewUserFromExternalLogin(info);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task Should_return_null_when_preferred_username_claim_is_missing()
    {
        // Arrange
        var info = CreateExternalLoginInfo(
            new Claim(ClaimTypes.Email, "john.doe@example.com"));
        var (sut, _) = CreateSubjectUnderTest(usernameValidatorResult: true, createAsyncResult: null, ExistingUser);

        // Act
        var result = await sut.CreateNewUserFromExternalLogin(info);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task Should_return_null_when_username_fails_validation()
    {
        // Arrange
        var info = CreateExternalLoginInfo(
            new Claim(ClaimTypes.Email, "john.doe@example.com"),
            new Claim("preferred_username", "invalid!user"));
        var (sut, _) = CreateSubjectUnderTest(usernameValidatorResult: false, createAsyncResult: null, ExistingUser);

        // Act
        var result = await sut.CreateNewUserFromExternalLogin(info);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task Should_create_a_new_normal_user()
    {
        // Arrange
        var info = CreateExternalLoginInfo(
            new Claim(ClaimTypes.Email, "john.doe@example.com"),
            new Claim("preferred_username", "john.doe"),
            new Claim("email_verified", "true"),
            new Claim("groups_direct", "some-unrelated-group"));
        var (sut, userManager) = CreateSubjectUnderTest(usernameValidatorResult: true, createAsyncResult: null, ExistingUser);

        // Act
        var result = await sut.CreateNewUserFromExternalLogin(info);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("john.doe", result.UserName);
        Assert.Equal("john.doe@example.com", result.Email);
        Assert.True(result.EmailConfirmed);
        Assert.Equal(DateTimeOffset.MinValue, result.PasswordExpirationDate);
        await userManager.DidNotReceive().AddToRoleAsync(Arg.Any<SuiteUser>(), AuthorizationConstants.AdminRoleName);
    }

    [Fact]
    public async Task Should_create_a_new_admin_if_respective_group_is_present()
    {
        // Arrange
        var info = CreateExternalLoginInfo(
            new Claim(ClaimTypes.Email, "admin@example.com"),
            new Claim("preferred_username", "admin.user"),
            new Claim("groups_direct", "vicione-oss"));
        var (sut, userManager) = CreateSubjectUnderTest(usernameValidatorResult: true, createAsyncResult: null, ExistingUser);

        // Act
        var result = await sut.CreateNewUserFromExternalLogin(info);

        // Assert
        Assert.NotNull(result);
        await userManager.Received(1).AddToRoleAsync(result, AuthorizationConstants.AdminRoleName);
    }

    [Fact]
    public async Task Should_return_null_when_email_is_malformed()
    {
        // Arrange
        var info = CreateExternalLoginInfo(
            new Claim(ClaimTypes.Email, "not-a-valid-email"),
            new Claim("preferred_username", "john.doe"));
        var (sut, userManager) = CreateSubjectUnderTest(
            usernameValidatorResult: true,
            createAsyncResult: IdentityResult.Failed(new IdentityError { Code = "InvalidEmail", Description = "Email is invalid" }),
            ExistingUser);

        // Act
        var result = await sut.CreateNewUserFromExternalLogin(info);

        // Assert
        Assert.Null(result);
        await userManager.Received(1).CreateAsync(Arg.Any<SuiteUser>());
        await userManager.DidNotReceive().AddLoginAsync(Arg.Any<SuiteUser>(), Arg.Any<UserLoginInfo>());
    }

    [Fact]
    public async Task Should_promote_first_user_to_admin_even_without_vicione_group()
    {
        // Arrange
        var info = CreateExternalLoginInfo(
            new Claim(ClaimTypes.Email, "first@example.com"),
            new Claim("preferred_username", "first.user"));
        var (sut, userManager) = CreateSubjectUnderTest(); // empty database — no existing users

        // Act
        var result = await sut.CreateNewUserFromExternalLogin(info);

        // Assert
        Assert.NotNull(result);
        await userManager.Received(1).AddToRoleAsync(result, AuthorizationConstants.AdminRoleName);
    }

    private static ExternalLoginInfo CreateExternalLoginInfo(params Claim[] claims)
    {
        var identity = new ClaimsIdentity(claims, LoginProvider);
        var principal = new ClaimsPrincipal(identity);
        return new ExternalLoginInfo(principal, LoginProvider, ProviderKey, LoginProvider);
    }

    private static (ExternalLoginService sut, UserManager<SuiteUser> userManager) CreateSubjectUnderTest(
        bool usernameValidatorResult = true,
        IdentityResult? createAsyncResult = null,
        params SuiteUser[] existingUsers)
    {
        var userManager = Substitute.For<UserManager<SuiteUser>>(
            Substitute.For<IUserStore<SuiteUser>>(),
            Substitute.For<IOptions<IdentityOptions>>(),
            Substitute.For<IPasswordHasher<SuiteUser>>(),
            Substitute.For<IEnumerable<IUserValidator<SuiteUser>>>(),
            Substitute.For<IEnumerable<IPasswordValidator<SuiteUser>>>(),
            Substitute.For<ILookupNormalizer>(),
            Substitute.For<IdentityErrorDescriber>(),
            Substitute.For<IServiceProvider>(),
            Substitute.For<ILogger<UserManager<SuiteUser>>>());

        userManager.Users.Returns(existingUsers.AsQueryable());

        userManager.CreateAsync(Arg.Any<SuiteUser>())
            .Returns(createAsyncResult ?? IdentityResult.Success);

        userManager.AddLoginAsync(Arg.Any<SuiteUser>(), Arg.Any<UserLoginInfo>())
            .Returns(IdentityResult.Success);

        userManager.AddToRoleAsync(Arg.Any<SuiteUser>(), Arg.Any<string>())
            .Returns(IdentityResult.Success);

        var usernameValidator = Substitute.For<IUsernameValidator>();
        usernameValidator
            .Validate(Arg.Any<string>(), out Arg.Any<string?>())
            .Returns(callInfo =>
            {
                callInfo[1] = usernameValidatorResult ? null : "Username is invalid";
                return usernameValidatorResult;
            });

        var logger = Substitute.For<ILogger<ExternalLoginService>>();
        return (new ExternalLoginService(userManager, usernameValidator, logger), userManager);
    }
}
