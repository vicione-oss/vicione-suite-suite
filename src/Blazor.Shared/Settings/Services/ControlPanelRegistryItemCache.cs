using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components.Authorization;
using Sdk.Client.ControlPanels.Services;
using Sdk.Client.Services;

namespace Blazor.Shared.Settings.Services;

internal sealed partial class ControlPanelRegistryItemCache
    : IControlPanelRegistryItemCache, IDisposable
{
    private readonly CancellationTokenSource _cancellationTokenSource = new();

    private readonly SemaphoreSlim _semaphore = new(1);

    private readonly IEnumerable<IControlPanelRegistry> _controlPanelRegistries;
    private readonly IEnumerable<IAuthorizationHandler> _authorizationHandlers;
    private readonly AuthenticationStateProvider _authenticationStateProvider;

    private ClaimsPrincipal? _user;
    private IEnumerable<IControlPanelRegistryItem>? _controlPanelRegistryItems;

    private CancellationToken CancellationToken => _cancellationTokenSource.Token;

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

    public void Dispose()
    {
        foreach (var r in _controlPanelRegistries)
            r.Changed -= ControlPanelRegistryChanged;

        _authenticationStateProvider.AuthenticationStateChanged -= AuthenticationStateChanged;

        _cancellationTokenSource.Cancel();
        _cancellationTokenSource.Dispose();

        _semaphore.Dispose();
    }

    private async void AuthenticationStateChanged(Task<AuthenticationState> authenticationStateTask)
    {
        var notifyChanged = false;

        await _semaphore.WaitAsync(CancellationToken);
        try
        {
            _user = null;
            _controlPanelRegistryItems = null;

            notifyChanged = true;
        }
        catch (OperationCanceledException)
        {
            // nothing to do here, we just return gracefully
        }
        finally
        {
            _semaphore.Release();
        }

        if (notifyChanged)
            Changed?.Invoke();
    }

    private async void ControlPanelRegistryChanged(RegistryChangedEventArgs<IControlPanelRegistryItem> args)
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
        catch (OperationCanceledException)
        {
            // nothing to do here, we just return gracefully
        }
        finally
        {
            _semaphore.Release();
        }

        if (notifyChanged)
            Changed?.Invoke();
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
                        await t.WaitAsync(cancellationToken);
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
        await _semaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (user != _user || _controlPanelRegistryItems is null)
            {
                _user = user;

                _controlPanelRegistryItems = await GetControlPanelRegistryItems(user, cancellationToken);
            }

            return _controlPanelRegistryItems;
        }
        catch (OperationCanceledException)
        {
            // nothing to do here, we just return gracefully

            return [];
        }
        finally
        {
            _semaphore.Release();
        }
    }
}
