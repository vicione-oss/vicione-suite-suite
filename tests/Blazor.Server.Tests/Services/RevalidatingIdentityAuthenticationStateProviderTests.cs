using System.Globalization;
using System.Security.Claims;
using AwesomeAssertions;
using Blazor.Server.Backend.Services;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;
using Sdk.Authorization;
using Sdk.Client.Infrastructure;
using Xunit;

namespace Blazor.Server.Tests.Services;

public sealed class RevalidatingIdentityAuthenticationStateProviderTests
{
    private readonly ILoggerFactory _loggerFactory = Substitute.For<ILoggerFactory>();
    private readonly IServiceScopeFactory _scopeFactory = Substitute.For<IServiceScopeFactory>();
    private readonly IOptions<IdentityOptions> _identityOptions = Substitute.For<IOptions<IdentityOptions>>();

    [Fact]
    public async Task Should_return_authentication_state_when_user_is_unknown()
    {
        // Arrange
        var option = new IdentityOptions();
        _identityOptions.Value.Returns(option);

        var scope = Substitute.For<IServiceScope>();
        _scopeFactory.CreateScope().Returns(scope);

        var services = new ServiceCollection();
        var userManager = GetUserManager(services);

        using var serviceProvider = services.BuildServiceProvider();
        scope.ServiceProvider.Returns(serviceProvider);

        // return null for unknown user
        _ = userManager.GetUserAsync(Arg.Any<ClaimsPrincipal>()).Returns(Task.FromResult<IdentityUser?>(null));

        var claimPrincipalUserAdmin = new ClaimsPrincipal(new ClaimsIdentity([
            new Claim(ClaimsIdentity.DefaultRoleClaimType, AccessLevel.Full.ToString())
        ]));
        var authenticationState = new AuthenticationState(claimPrincipalUserAdmin);

        using var stateProvider = new MockRevalidatingIdentityAuthenticationStateProvider<IdentityUser>(
            _loggerFactory, _scopeFactory, _identityOptions);
        // Act
        var validatedState = await stateProvider.ValidateAuthenticationStateAsync(authenticationState, TestContext.Current.CancellationToken);

        // Assert
        _ = userManager.Received(1).GetUserAsync(Arg.Is<ClaimsPrincipal>(cp => cp == claimPrincipalUserAdmin));
        validatedState.Should().BeFalse();
    }

    [Fact]
    public async Task Should_return_authentication_state_when_user_is_common_and_supports_user_security_stamp_is_false()
    {
        // Arrange
        var option = new IdentityOptions();
        _identityOptions.Value.Returns(option);

        var scope = Substitute.For<IServiceScope>();
        _scopeFactory.CreateScope().Returns(scope);

        var services = new ServiceCollection();
        var userManager = GetUserManager(services);

        using var serviceProvider = services.BuildServiceProvider();
        scope.ServiceProvider.Returns(serviceProvider);

        var identityUserAdmin = Substitute.For<IdentityUser>();
        _ = userManager.GetUserAsync(Arg.Any<ClaimsPrincipal>()).Returns(Task.FromResult<IdentityUser?>(identityUserAdmin));
        userManager.SupportsUserSecurityStamp.Returns(false);

        var claimPrincipalUserAdmin = new ClaimsPrincipal(new ClaimsIdentity([
            new Claim(ClaimsIdentity.DefaultRoleClaimType, AccessLevel.Full.ToString())
        ]));
        var authenticationState = new AuthenticationState(claimPrincipalUserAdmin);

        using var stateProvider = new MockRevalidatingIdentityAuthenticationStateProvider<IdentityUser>(
            _loggerFactory, _scopeFactory, _identityOptions);
        // Act
        var validatedState = await stateProvider.ValidateAuthenticationStateAsync(authenticationState, TestContext.Current.CancellationToken);

        // Assert
        _ = userManager.Received(1).GetUserAsync(Arg.Is<ClaimsPrincipal>(cp => cp == claimPrincipalUserAdmin));
        validatedState.Should().BeTrue();
    }

    [Fact]
    public async Task Should_return_authentication_state_when_user_is_common_and_supports_user_security_stamp_is_true()
    {
        // Arrange
        var option = new IdentityOptions();
        _identityOptions.Value.Returns(option);

        var scope = Substitute.For<IServiceScope>();
        _scopeFactory.CreateScope().Returns(scope);

        var services = new ServiceCollection();
        var userManager = GetUserManager(services);

        using var serviceProvider = services.BuildServiceProvider();
        scope.ServiceProvider.Returns(serviceProvider);

        var claimPrincipalUserAdmin = new ClaimsPrincipal(new ClaimsIdentity([
            new Claim(ClaimsIdentity.DefaultRoleClaimType, AccessLevel.Full.ToString())
        ]));
        var securityStamp = GenerateSecurityStamp();

        var identityUserAdmin = Substitute.For<IdentityUser>();
        _ = userManager.GetUserAsync(Arg.Any<ClaimsPrincipal>()).Returns(Task.FromResult<IdentityUser?>(identityUserAdmin));
        userManager.SupportsUserSecurityStamp.Returns(true);
        userManager.GetSecurityStampAsync(Arg.Any<IdentityUser>()).Returns(Task.FromResult(securityStamp));

        // SecurityClaim für User erstellen
        var claimIdentity = claimPrincipalUserAdmin.Identities.First();
        claimIdentity.AddClaim(new Claim(option.ClaimsIdentity.SecurityStampClaimType, securityStamp));

        var authenticationState = new AuthenticationState(claimPrincipalUserAdmin);

        using var stateProvider = new MockRevalidatingIdentityAuthenticationStateProvider<IdentityUser>(
            _loggerFactory, _scopeFactory, _identityOptions);
        // Act
        var validatedState = await stateProvider.ValidateAuthenticationStateAsync(authenticationState, TestContext.Current.CancellationToken);

        // Assert
        _ = userManager.Received(1).GetUserAsync(Arg.Is<ClaimsPrincipal>(cp => cp == claimPrincipalUserAdmin));
        validatedState.Should().BeTrue();
    }

    private static UserManager<IdentityUser> GetUserManager(ServiceCollection services)
    {
        var userStore = Substitute.For<IUserStore<IdentityUser>>();
        var userManager = Substitute.For<UserManager<IdentityUser>>(
            userStore,
            Substitute.For<IOptions<IdentityOptions>>(),
            Substitute.For<IPasswordHasher<IdentityUser>>(),
            Substitute.For<IEnumerable<IUserValidator<IdentityUser>>>(),
            Substitute.For<IEnumerable<IPasswordValidator<IdentityUser>>>(),
            Substitute.For<ILookupNormalizer>(),
            Substitute.For<IdentityErrorDescriber>(),
            Substitute.For<IServiceProvider>(),
            Substitute.For<ILogger<UserManager<IdentityUser>>>());
        services.AddSingleton(userManager);
        return userManager;
    }

    private static string GenerateSecurityStamp()
    {
        var guid = Guid.NewGuid();
        return string.Concat(Array.ConvertAll(guid.ToByteArray(), b => b.ToString("X2", CultureInfo.InvariantCulture.NumberFormat)));
    }

    private class MockRevalidatingIdentityAuthenticationStateProvider<TUser>(ILoggerFactory loggerFactory,
        IServiceScopeFactory scopeFactory, IOptions<IdentityOptions> optionsAccessor)
            : RevalidatingIdentityAuthenticationStateProvider<TUser>(loggerFactory, scopeFactory, optionsAccessor, Substitute.For<IUiMediator>())
                where TUser : class
    {
        public new async Task<bool> ValidateAuthenticationStateAsync(
            AuthenticationState authenticationState, CancellationToken cancellationToken)
            => await base.ValidateAuthenticationStateAsync(authenticationState, cancellationToken);
    }
}
