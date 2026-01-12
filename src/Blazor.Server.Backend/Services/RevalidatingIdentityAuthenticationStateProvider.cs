using System.Security.Claims;
using Core.Shared.UserManagement.Events;
using Core.Shared.UserManagement.Extensions;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Server;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Sdk.Client.Infrastructure;
using Sdk.UserManagement.Contracts;
using Sdk.UserManagement.Events;
using Sdk.Utils;

namespace Blazor.Server.Backend.Services;

public class RevalidatingIdentityAuthenticationStateProvider<TUser>
    : RevalidatingServerAuthenticationStateProvider, IEventConsumer<UserUpdatedEvent>, IEventConsumer<UserDeletedEvent>, IEventConsumer<RoleUpdatedEvent>, IEventConsumer<RoleDeletedEvent>
        where TUser : class
{
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly IdentityOptions _options;
    private readonly AutoDisposeList<IDisposable> _subscriptionHandle = [];

    protected override TimeSpan RevalidationInterval => TimeSpan.FromMinutes(30);

    public RevalidatingIdentityAuthenticationStateProvider(ILoggerFactory loggerFactory, IServiceScopeFactory serviceScopeFactory,
        IOptions<IdentityOptions> optionsAccessor, IUiMediator uiMediator)
            : base(loggerFactory)
    {
        _serviceScopeFactory = serviceScopeFactory;
        _options = optionsAccessor.Value;

        _subscriptionHandle.Add(uiMediator.Register<UserUpdatedEvent>(this));
        _subscriptionHandle.Add(uiMediator.Register<UserDeletedEvent>(this));
        _subscriptionHandle.Add(uiMediator.Register<RoleUpdatedEvent>(this));
        _subscriptionHandle.Add(uiMediator.Register<RoleDeletedEvent>(this));
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
            _subscriptionHandle.Dispose();

        base.Dispose(disposing);
    }

    protected override async Task<bool> ValidateAuthenticationStateAsync(
        AuthenticationState authenticationState, CancellationToken cancellationToken)
    {
        // Get the user manager from a new scope to ensure it fetches fresh data
        await using var serviceScope = _serviceScopeFactory.CreateAsyncScope();

        var userManager = serviceScope.ServiceProvider.GetRequiredService<UserManager<TUser>>();

        return await ValidateSecurityStamp(userManager, authenticationState.User);
    }

    private async Task<bool> ValidateSecurityStamp(UserManager<TUser> userManager, ClaimsPrincipal principal)
    {
        var user = await userManager.GetUserAsync(principal);
        if (user is null)
        {
            return false;
        }
        else if (!userManager.SupportsUserSecurityStamp)
        {
            return true;
        }
        else
        {
            var principalStamp = principal.FindFirstValue(_options.ClaimsIdentity.SecurityStampClaimType);
            var userStamp = await userManager.GetSecurityStampAsync(user);
            return principalStamp == userStamp;
        }
    }

    public async Task Consume(ClientContext<UserUpdatedEvent> context, CancellationToken cancellationToken)
    {
        var authenticationState = await GetAuthenticationStateAsync();

        var userProfile = context.Message.UserProfile;

        if (!authenticationState.User.IsAssociatedWith(userProfile))
            return;

        if (!userProfile.IsAuthorizationChanged(context.Message.UserProfileBefore))
            return;

        await using var serviceScope = _serviceScopeFactory.CreateAsyncScope();

        var userManager = serviceScope.ServiceProvider.GetRequiredService<UserManager<TUser>>();
        var claimsPrincipalFactory = serviceScope.ServiceProvider.GetRequiredService<IUserClaimsPrincipalFactory<TUser>>();

        var suiteUser = await userManager.FindByNameAsync(userProfile.UserName.Value);
        if (suiteUser is null)
            return;

        var newUser = await claimsPrincipalFactory.CreateAsync(suiteUser);

        var newAuthenticationState = new AuthenticationState(newUser);
        SetAuthenticationState(Task.FromResult(newAuthenticationState));
    }

    public async Task Consume(ClientContext<UserDeletedEvent> context, CancellationToken cancellationToken)
    {
        var authenticationState = await GetAuthenticationStateAsync();

        if (authenticationState.User.IsAssociatedWith(context.Message.UserProfile))
            ForceSignOut();
    }

    public async Task Consume(ClientContext<RoleUpdatedEvent> context, CancellationToken cancellationToken)
        => await RoleUpdatedOrDeleted(context.Message.Role);

    public async Task Consume(ClientContext<RoleDeletedEvent> context, CancellationToken cancellationToken)
        => await RoleUpdatedOrDeleted(context.Message.Role);

    /// <remarks>
    /// Adopted from <see cref="RevalidatingServerAuthenticationStateProvider"/>
    /// </remarks>
    private void ForceSignOut()
    {
        var anonymousUser = new ClaimsPrincipal(new ClaimsIdentity());
        var anonymousState = new AuthenticationState(anonymousUser);
        SetAuthenticationState(Task.FromResult(anonymousState));
    }

    private async Task RoleUpdatedOrDeleted(Role role)
    {
        var authenticationState = await GetAuthenticationStateAsync();

        if (!authenticationState.User?.IsInRole(role.Name ?? string.Empty) ?? false)
            return;

        await using var serviceScope = _serviceScopeFactory.CreateAsyncScope();

        var userManager = serviceScope.ServiceProvider.GetRequiredService<UserManager<TUser>>();
        var claimsPrincipalFactory = serviceScope.ServiceProvider.GetRequiredService<IUserClaimsPrincipalFactory<TUser>>();

        var suiteUser = await userManager.FindByNameAsync(authenticationState.User?.Identity?.Name ?? string.Empty);
        if (suiteUser is null)
            return;

        var newUser = await claimsPrincipalFactory.CreateAsync(suiteUser);

        var newAuthenticationState = new AuthenticationState(newUser);
        SetAuthenticationState(Task.FromResult(newAuthenticationState));
    }
}
