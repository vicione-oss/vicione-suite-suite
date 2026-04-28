using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components.Authorization;
using Sdk.Client.ControlPanels.Services;
using Sdk.Client.Services;

namespace Blazor.Shared.Settings.Services;

internal sealed partial class ControlPanelRegistryItemCache : IControlPanelRegistryItemCache, IAsyncDisposable
{
    private readonly CancellationTokenSource _semaphoreCancellationTokenSource = new();
    private readonly SemaphoreSlim _semaphore = new(1);

    private readonly IEnumerable<IControlPanelRegistry> _controlPanelRegistries;
    private readonly IEnumerable<IAuthorizationHandler> _authorizationHandlers;
    private readonly AuthenticationStateProvider _authenticationStateProvider;

    private ClaimsPrincipal? _user;
    private IEnumerable<IControlPanelRegistryItem>? _controlPanelRegistryItems;

    private bool _disposedAsync;

    private CancellationToken CancellationToken => _semaphoreCancellationTokenSource.Token;

    public event Action? Changed;

    public ControlPanelRegistryItemCache(IEnumerable<IControlPanelRegistry> controlPanelRegistries,
        IEnumerable<IAuthorizationHandler> authorizationHandlers,
        AuthenticationStateProvider authenticationStateProvider)
    {
        _controlPanelRegistries = controlPanelRegistries;
        _authorizationHandlers = authorizationHandlers;

        _authenticationStateProvider = authenticationStateProvider;
        _authenticationStateProvider.AuthenticationStateChanged += AuthenticationStateChanged;

        foreach (var r in _controlPanelRegistries)
            r.Changed += ControlPanelRegistryChanged;
    }

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.CompareExchange(ref _disposedAsync, true, false))
            return;

        foreach (var r in _controlPanelRegistries)
            r.Changed -= ControlPanelRegistryChanged;

        _authenticationStateProvider.AuthenticationStateChanged -= AuthenticationStateChanged;

        await _semaphoreCancellationTokenSource.CancelAsync();
        _semaphoreCancellationTokenSource.Dispose();

        _semaphore.Dispose();
    }

    private async void AuthenticationStateChanged(Task<AuthenticationState> authenticationStateTask)
    {
        if (_disposedAsync)
            return;

        try
        {
            var notifyChanged = false;

            await _semaphore.WaitAsync(CancellationToken).ConfigureAwait(false);
            try
            {
                _user = null;
                _controlPanelRegistryItems = null;

                notifyChanged = true;
            }
            finally
            {
                _semaphore.Release();
            }

            if (notifyChanged)
                Changed?.Invoke();
        }
        catch (OperationCanceledException)
        {
            // Nothing to do here, return gracefully
        }
        catch (ObjectDisposedException)
        {
            // Semaphore or other object already disposed, nothing we can do, return gracefully
        }
    }

    private async void ControlPanelRegistryChanged(RegistryChangedEventArgs<IControlPanelRegistryItem> args)
    {
        if (_disposedAsync)
            return;

        try
        {
            var notifyChanged = false;

            await _semaphore.WaitAsync(CancellationToken).ConfigureAwait(false);
            try
            {
                if (args.ItemsAdded.Any())
                {
                    _controlPanelRegistryItems = null;

                    notifyChanged = true;
                }
                else
                {
                    if (_controlPanelRegistryItems is not null && _controlPanelRegistryItems.Intersect(args.ItemsRemoved).Any())
                    {
                        _controlPanelRegistryItems = null;

                        notifyChanged = true;
                    }
                }
            }
            finally
            {
                _semaphore.Release();
            }

            if (notifyChanged)
                Changed?.Invoke();
        }
        catch (OperationCanceledException)
        {
            // Nothing to do here, return gracefully
        }
        catch (ObjectDisposedException)
        {
            // Semaphore or other object already disposed, nothing we can do, return gracefully
        }
    }

    private async Task<IEnumerable<IControlPanelRegistryItem>> GetControlPanelRegistryItems(ClaimsPrincipal? user,
        CancellationToken cancellationToken)
    {
        var result = new List<IControlPanelRegistryItem>();

        foreach (var controlPanelRegistry in _controlPanelRegistries)
        {
            foreach (var controlPanelRegistryItem in controlPanelRegistry)
            {
                if (controlPanelRegistryItem.AuthorizationRequirement is not null)
                {
                    if (user is null)
                        continue;

                    var authorizationHandlerContext = new AuthorizationHandlerContext([controlPanelRegistryItem.AuthorizationRequirement], user, resource: null);

                    var authorizationHandleTasks = _authorizationHandlers.Select(h => h.HandleAsync(authorizationHandlerContext));

                    var t = Task.WhenAll(authorizationHandleTasks);
                    try
                    {
                        await t.WaitAsync(cancellationToken).ConfigureAwait(false);
                    }
                    catch (OperationCanceledException)
                    {
                        // nothing to do here, we just return gracefully
                    }

                    if (!cancellationToken.IsCancellationRequested)
                    {
                        if (authorizationHandlerContext.HasSucceeded)
                            result.Add(controlPanelRegistryItem);
                    }
                }
                else
                {
                    result.Add(controlPanelRegistryItem);
                }
            }
        }

        return result;
    }

    public async Task<IEnumerable<IControlPanelRegistryItem>> GetAll(ClaimsPrincipal? user, CancellationToken cancellationToken = default)
    {
        if (_disposedAsync)
            return [];

        try
        {
            using var linkedCancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(CancellationToken, cancellationToken);

            await _semaphore.WaitAsync(linkedCancellationTokenSource.Token).ConfigureAwait(false);
            try
            {
                if (user != _user || _controlPanelRegistryItems is null)
                {
                    _user = user;

                    _controlPanelRegistryItems = await GetControlPanelRegistryItems(user, cancellationToken);
                }

                return _controlPanelRegistryItems;
            }
            finally
            {
                _semaphore.Release();
            }
        }
        catch (OperationCanceledException)
        {
            // Nothing to do here, return gracefully

            return [];
        }
        catch (ObjectDisposedException)
        {
            // Semaphore or other object already disposed, nothing we can do, return gracefully

            return [];
        }
    }
}
