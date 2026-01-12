using Blazor.Shared.UserManagement.Services;
using Core.Shared.Instance.Contracts;
using Core.Shared.Instance.Events;
using Core.Shared.Instance.Requests;
using Core.Shared.UserManagement.Contracts;
using Core.Shared.UserManagement.Events;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.Logging;
using Sdk.Client.Infrastructure;
using Sdk.Utils;

namespace Blazor.Shared.Services;

public sealed class ClientTimeProvider : TimeProvider,
        IEventConsumer<CrossInstanceConfigurationChanged>,
        IEventConsumer<UserUpdatedEvent>,
        IClientTimeProvider,
        IAsyncDisposable
{
    private TimeZoneInfo? _userTimeZone;
    private TimeZoneInfo? _systemTimeZone;

    private readonly SemaphoreSlim _semaphore = new(1);
    private readonly CancellationTokenSource _cancellationTokenSource = new();
    private readonly AutoDisposeList<IDisposable> _disposables = new();
    private readonly IUserService _userService;
    private readonly IUiMediator _uiMediator;
    private readonly AuthenticationStateProvider _authenticationStateProvider;
    private readonly ILogger<ClientTimeProvider> _logger;

    public override TimeZoneInfo LocalTimeZone => _userTimeZone ?? _systemTimeZone ?? base.LocalTimeZone;

    public ClientTimeProvider(IUserService userService,
        IUiMediator uiMediator,
        AuthenticationStateProvider authenticationStateProvider,
        ILogger<ClientTimeProvider> logger)
    {
        _userService = userService;
        _uiMediator = uiMediator;
        _authenticationStateProvider = authenticationStateProvider;
        _logger = logger;
        _disposables.Add(_uiMediator.Register<CrossInstanceConfigurationChanged>(this));
        _disposables.Add(_uiMediator.Register<UserUpdatedEvent>(this));
    }

    public async Task Initialize(CancellationToken? cancellationToken = null)
    {
        var authState = await _authenticationStateProvider.GetAuthenticationStateAsync();

        _authenticationStateProvider.AuthenticationStateChanged -= OnAuthenticationStateChanged;
        _authenticationStateProvider.AuthenticationStateChanged += OnAuthenticationStateChanged;

        await SetClientTimeZone(authState, cancellationToken);
    }

    private async Task SetClientTimeZone(AuthenticationState authState, CancellationToken? cancellationToken = null)
    {
        try
        {
            try
            {
                var ct = cancellationToken ?? _cancellationTokenSource.Token;
                await _semaphore.WaitAsync(ct);

                var users = await _userService.GetUsers(new UserName(authState.User.Identity?.Name), ct);
                SetUserTimeZone(users.FirstOrDefault());

                var response = await _uiMediator
                    .Request<GetCrossInstanceConfiguration, GetCrossInstanceConfigurationResponse>(new GetCrossInstanceConfiguration(), ct);
                SetSystemTimeZone(response.CrossInstanceConfiguration);
            }
            finally
            {
                _semaphore.Release();
            }
        }
        catch (OperationCanceledException)
        {
            // Nothing to do here, return gracefully
        }
        catch (ObjectDisposedException)
        {
            // Semaphore already disposed, nothing we can do, return gracefully
        }
    }

    public async ValueTask DisposeAsync()
    {
        _disposables.Dispose();
        _authenticationStateProvider.AuthenticationStateChanged -= OnAuthenticationStateChanged;

        try
        {
            await _cancellationTokenSource.CancelAsync();
        }
        catch (ObjectDisposedException)
        {
            // ignore
        }

        _cancellationTokenSource.Dispose();

        _semaphore.Dispose();
    }

    private async void OnAuthenticationStateChanged(Task<AuthenticationState> task)
    {
        try
        {
            var authState = await task;
            await SetClientTimeZone(authState);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Authentication state changed failed");
        }
    }

    private void SetUserTimeZone(UserProfile? userProfile)
    {
        var timeZoneId = userProfile?.TimeZone;
        if (timeZoneId is null)
        {
            _userTimeZone = null;
            return;
        }

        if (!TimeZoneInfo.TryFindSystemTimeZoneById(timeZoneId, out var tz))
        {
            _userTimeZone = null;
            _logger.LogWarning("'{TimeZoneId}' is not a known time zone", timeZoneId);
        }
        else
        {
            _userTimeZone = tz;
        }
    }

    private void SetSystemTimeZone(CrossInstanceConfiguration crossConfig)
    {
        var timeZoneId = crossConfig.TimeZoneId;
        if (!TimeZoneInfo.TryFindSystemTimeZoneById(timeZoneId, out var tz))
            _logger.LogWarning("'{TimeZoneId}' is not a known time zone", timeZoneId);
        else
            _systemTimeZone = tz;
    }

    public Task Consume(ClientContext<CrossInstanceConfigurationChanged> context,
        CancellationToken cancellationToken)
    {
        SetSystemTimeZone(context.Message.CrossInstanceConfiguration);
        return Task.CompletedTask;
    }

    public Task Consume(ClientContext<UserUpdatedEvent> context,
        CancellationToken cancellationToken)
    {
        SetUserTimeZone(context.Message.UserProfile);
        return Task.CompletedTask;
    }
}
