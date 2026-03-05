using System.Security.Claims;
using Blazor.Shared.Authorization.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Sdk.Client.Modules;
using Sdk.Client.NavTiles.Enums;
using Sdk.Client.NavTiles.Services;

namespace Blazor.Shared.NavTiles.Components;

public sealed partial class NavTilePanel : ComponentBase, IDisposable
{
    private readonly CancellationTokenSource _cancellationTokenSource = new();
    private readonly SemaphoreSlim _semaphore = new(1);

    private List<INavTileRegistryItem>? _navTiles;

    private class NavTileLinkTargetComparer : IComparer<string?>
    {
        public int Compare(string? x, string? y)
        {
            // tiles with no route should come last
            if (x is null && y is null)
                return 0;

            if (x is null)
                return 1;

            if (y is null)
                return -1;

            return string.Compare(x, y, StringComparison.Ordinal);
        }
    }

    private CancellationToken CancellationToken => _cancellationTokenSource.Token;

    [Parameter] public RenderFragment? ChildContent { get; set; }
    [Parameter] public string? Heading { get; set; }
    [Parameter] public NavTileGroup Group { get; set; }

    [Inject] private IEnumerable<INavTileRegistry<IClientModule>> NavTileRegistries { get; set; } = default!;
    [Inject] private IEnumerable<IAuthorizationHandler> AuthorizationHandlers { get; set; } = default!;
    [Inject] private AuthenticationStateProvider AuthenticationStateProvider { get; set; } = default!;

    protected override async Task OnInitializedAsync()
    {
        var user = await AuthenticationStateProvider.GetUser();

        await UpdateNavTiles(user);

        // this one triggers a a lot of refresh cycles
        // because it's triggered on add nav tile
        foreach (var r in NavTileRegistries)
            r.Changed += NavTileRegistryChanged;

        AuthenticationStateProvider.AuthenticationStateChanged += AuthenticationStateChanged;
    }

    public void Dispose()
    {
        AuthenticationStateProvider.AuthenticationStateChanged -= AuthenticationStateChanged;

        foreach (var r in NavTileRegistries)
            r.Changed -= NavTileRegistryChanged;

        _cancellationTokenSource.Cancel();
        _cancellationTokenSource.Dispose();

        _semaphore.Dispose();
    }

    private async void NavTileRegistryChanged()
    {
        var notifyStateHasChanged = false;

        await _semaphore.WaitAsync(CancellationToken);
        try
        {
            var user = await AuthenticationStateProvider.GetUser();

            await UpdateNavTiles(user);

            notifyStateHasChanged = true;
        }
        catch (OperationCanceledException)
        {
            // nothing to do here, we just return gracefully
        }
        finally
        {
            _semaphore.Release();
        }

        if (notifyStateHasChanged)
            await InvokeAsync(StateHasChanged);
    }

    private async void AuthenticationStateChanged(Task<AuthenticationState> authenticationStateTask)
    {
        var notifyStateHasChanged = false;

        await _semaphore.WaitAsync(CancellationToken);
        try
        {
            var authenticationState = await authenticationStateTask.WaitAsync(CancellationToken);

            await UpdateNavTiles(authenticationState.User);

            notifyStateHasChanged = true;
        }
        catch (OperationCanceledException)
        {
            // nothing to do here, we just return gracefully
        }
        finally
        {
            _semaphore.Release();
        }

        if (notifyStateHasChanged)
            await InvokeAsync(StateHasChanged);
    }

    private async Task UpdateNavTiles(ClaimsPrincipal? user)
    {
        var navTiles = await GetNavTiles(user);

        _navTiles ??= [];
        _navTiles.Clear();
        _navTiles.AddRange(navTiles);
    }

    private async Task<IEnumerable<INavTileRegistryItem>> GetNavTiles(ClaimsPrincipal? user)
    {
        if (user is null)
            return [];

        var result = new List<INavTileRegistryItem>();

        foreach (var navTileRegistryItem in NavTileRegistries.SelectMany(r => r.Where(item => item.Group == Group)))
        {
            if (navTileRegistryItem.AuthorizationRequirement is not null)
            {
                var authorizationHandlerContext = new AuthorizationHandlerContext([navTileRegistryItem.AuthorizationRequirement], user, resource: null);

                var authorizationHandleTasks = AuthorizationHandlers.Select(h => h.HandleAsync(authorizationHandlerContext));

                var t = Task.WhenAll(authorizationHandleTasks);
                try
                {
                    await t.WaitAsync(CancellationToken);
                }
                catch (OperationCanceledException)
                {
                    // nothing to do here, we just return gracefully
                }

                if (!CancellationToken.IsCancellationRequested)
                {
                    if (authorizationHandlerContext.HasSucceeded)
                        result.Add(navTileRegistryItem);
                }
            }
            else
            {
                result.Add(navTileRegistryItem);
            }
        }

        return result.OrderBy(d => d.State.LinkTarget, new NavTileLinkTargetComparer());
    }
}
