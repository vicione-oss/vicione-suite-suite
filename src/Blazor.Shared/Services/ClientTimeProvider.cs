using Blazor.Shared.UserManagement.Services;
using Core.Shared.Instance.Requests;
using Core.Shared.UserManagement.Contracts;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.Logging;
using Sdk.Client.Infrastructure;

namespace Blazor.Shared.Services;

public sealed class ClientTimeProvider(IUserService userService,
    IUiMediator uiMediator,
    AuthenticationStateProvider authenticationStateProvider,
    ILogger<ClientTimeProvider> logger) : TimeProvider, IClientTimeProvider, IDisposable
{
    private TimeZoneInfo? _timeZone;

    public override TimeZoneInfo LocalTimeZone => _timeZone ?? base.LocalTimeZone;

    public async Task Initialize(CancellationToken cancellationToken = default)
    {
        var authState = await authenticationStateProvider.GetAuthenticationStateAsync();
        var users = await userService.GetUsers(new UserName(authState.User.Identity?.Name), cancellationToken);
        authenticationStateProvider.AuthenticationStateChanged += OnAuthenticationStateChanged;

        var timeZoneId = users.FirstOrDefault()?.TimeZone;
        if (timeZoneId is null)
        {
            var response = await uiMediator.Request<GetCrossInstanceConfiguration, GetCrossInstanceConfigurationResponse>(
                        new GetCrossInstanceConfiguration(), cancellationToken);
            timeZoneId = response.CrossInstanceConfiguration.TimeZoneId;
        }
        _ = TimeZoneInfo.TryFindSystemTimeZoneById(timeZoneId, out _timeZone);
    }

    public void Dispose() => authenticationStateProvider.AuthenticationStateChanged -= OnAuthenticationStateChanged;

    private async void OnAuthenticationStateChanged(Task<AuthenticationState> task)
    {
        try
        {
            await Initialize();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Authentication state change failed");
        }
    }
}
