using System.Security.Claims;
using Blazor.Shared.Authorization.Extensions;
using Blazor.Shared.MessageBanner.NotificationArea;
using Blazor.Shared.Profile.NotificationArea;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Sdk.Client.NotificationArea.Services;
using Sdk.Client.Services;

namespace Blazor.Shared.NotificationArea.Components;

public sealed partial class NotificationElementGrid : ComponentBase, IDisposable
{
    private readonly List<INotificationElementRegistryItem> _notificationElementRegistryItems = [];
    private readonly CancellationTokenSource _cancellationTokenSource = new();
    private readonly SemaphoreSlim _semaphore = new(1);

    private CancellationToken CancellationToken => _cancellationTokenSource.Token;

    [Inject] public required IEnumerable<INotificationElementRegistry> NotificationElementRegistries { get; set; }
    [Inject] public required IActiveNotificationElementPolicy ActiveNotificationElementPolicy { get; set; }
    [Inject] public required IEnumerable<IAuthorizationHandler> AuthorizationHandlers { get; set; }
    [Inject] public required AuthenticationStateProvider AuthenticationStateProvider { get; set; }


    [Parameter] public string? CssClass { get; set; }

    protected override async Task OnInitializedAsync()
    {
        var user = await AuthenticationStateProvider.GetUser();

        await UpdateNotificationElements(user);

        foreach (var r in NotificationElementRegistries)
            r.Changed += NotificationElementRegistryChanged;

        AuthenticationStateProvider.AuthenticationStateChanged += AuthenticationStateChanged;
    }

    public void Dispose()
    {
        AuthenticationStateProvider.AuthenticationStateChanged -= AuthenticationStateChanged;

        foreach (var r in NotificationElementRegistries)
        {
            r.Changed -= NotificationElementRegistryChanged;

            foreach (var i in r)
                ActiveNotificationElementPolicy.Exclude(i.State);
        }

        _cancellationTokenSource.Cancel();
        _cancellationTokenSource.Dispose();

        _semaphore.Dispose();
    }

    private async Task UpdateNotificationElements(ClaimsPrincipal? user)
    {
        foreach (var notificationElementRegistryItem in _notificationElementRegistryItems)
            ActiveNotificationElementPolicy.Exclude(notificationElementRegistryItem.State);

        _notificationElementRegistryItems.Clear();

        foreach (var r in NotificationElementRegistries)
        {
            foreach (var i in r)
            {
                if (await IsAuthorized(i, user))
                    AddNotificationElement(i);
            }
        }

        _notificationElementRegistryItems.Sort(new NotificationElementRegistryItemComparer());
    }

    private async void NotificationElementRegistryChanged(RegistryChangedEventArgs<INotificationElementRegistryItem> args)
    {
        var notifyStateHasChanged = false;

        try
        {
            await _semaphore.WaitAsync(CancellationToken);
            try
            {
                var user = await AuthenticationStateProvider.GetUser();

                foreach (var itemAdded in args.ItemsAdded)
                {
                    if (await IsAuthorized(itemAdded, user))
                        AddNotificationElement(itemAdded);
                }

                foreach (var itemRemoved in args.ItemsRemoved)
                {
                    ActiveNotificationElementPolicy.Exclude(itemRemoved.State);
                    _notificationElementRegistryItems.Remove(itemRemoved);
                }

                _notificationElementRegistryItems.Sort(new NotificationElementRegistryItemComparer());

                notifyStateHasChanged = true;
            }
            finally
            {
                _semaphore.Release();
            }
        }
        catch (OperationCanceledException)
        {
            // Nothing to do here, we return gracefully
        }
        catch (ObjectDisposedException)
        {
            // Semaphore or other object already disposed, nothing we can do, return gracefully
        }

        if (notifyStateHasChanged)
            await InvokeAsync(StateHasChanged);
    }

    private async void AuthenticationStateChanged(Task<AuthenticationState> authenticationStateTask)
    {
        var notifyStateHasChanged = false;

        try
        {
            await _semaphore.WaitAsync(CancellationToken);
            try
            {
                var authenticationState = await authenticationStateTask.WaitAsync(CancellationToken);

                await UpdateNotificationElements(authenticationState.User);

                notifyStateHasChanged = true;
            }
            finally
            {
                _semaphore.Release();
            }
        }
        catch (OperationCanceledException)
        {
            // Nothing to do here, we return gracefully
        }
        catch (ObjectDisposedException)
        {
            // Semaphore or other object already disposed, nothing we can do, return gracefully
        }

        if (notifyStateHasChanged)
            await InvokeAsync(StateHasChanged);
    }

    private async Task<bool> IsAuthorized(INotificationElementRegistryItem notificationElementRegistryItem, ClaimsPrincipal? user)
    {
        if (notificationElementRegistryItem.AuthorizationRequirement is null)
            return true;

        if (user is null)
            return false;

        var authorizationHandlerContext = new AuthorizationHandlerContext([notificationElementRegistryItem.AuthorizationRequirement], user, resource: null);

        var authorizationHandleTasks = AuthorizationHandlers.Select(h => h.HandleAsync(authorizationHandlerContext));

        var t = Task.WhenAll(authorizationHandleTasks);
        try
        {
            await t.WaitAsync(CancellationToken);
        }
        catch (OperationCanceledException)
        {
            // Nothing to do here.
        }

        if (!CancellationToken.IsCancellationRequested)
            return authorizationHandlerContext.HasSucceeded;

        return false;
    }

    private void AddNotificationElement(INotificationElementRegistryItem notificationElementRegistryItem)
    {
        ActiveNotificationElementPolicy.Include(notificationElementRegistryItem.State);

        _notificationElementRegistryItems.Add(notificationElementRegistryItem);
    }

    private class NotificationElementRegistryItemComparer : Comparer<INotificationElementRegistryItem>
    {
        public override int Compare(INotificationElementRegistryItem? x, INotificationElementRegistryItem? y)
        {
            if (x is null || y is null)
                return 0;

            if (x.ComponentType == typeof(MessageBannerNotificationElement) || y.ComponentType == typeof(ProfileNotificationElement))
                return -1;

            if (x.ComponentType == typeof(ProfileNotificationElement) || y.ComponentType == typeof(MessageBannerNotificationElement))
                return 1;

            if (x.Position.CompareTo(y.Position) != 0)
                return x.Position.CompareTo(y.Position);

            return string.Compare(x.ComponentType.FullName, y.ComponentType.FullName, StringComparison.OrdinalIgnoreCase);
        }
    }
}
