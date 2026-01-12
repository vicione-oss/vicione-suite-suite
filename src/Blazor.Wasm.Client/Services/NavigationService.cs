using Blazor.Shared.Services;
using Blazor.Wasm.Client.Infrastructure.Security;
using Microsoft.AspNetCore.Components;
using Sdk.Client.Services;

namespace Blazor.Wasm.Client.Services;

internal sealed class NavigationService : INavigationService, IDisposable
{
    private readonly ILayoutService _layoutService;
    private readonly CustomAuthStateProvider _authStateProvider;

    public NavigationManager NavManager { get; }

    public NavigationService(
        NavigationManager navigationManager,
        ILayoutService layoutService,
        CustomAuthStateProvider authStateProvider)
    {
        NavManager = navigationManager;
        NavManager.LocationChanged += NavigationManager_LocationChanged;
        _layoutService = layoutService;
        _authStateProvider = authStateProvider;
    }

    public void Dispose() => NavManager.LocationChanged -= NavigationManager_LocationChanged;

    public void NavigateToRootPage(bool forceLoad = false) => NavManager.NavigateTo(NavManager.BaseUri, forceLoad);

    private void NavigationManager_LocationChanged(object? sender, Microsoft.AspNetCore.Components.Routing.LocationChangedEventArgs e)
    {
        if (e.Location == NavManager.BaseUri)
        {
            _layoutService.TitleBarAppName = string.Empty;
            _layoutService.IsLoadingOverlayVisible = false;
        }
    }

    public void RedirectToSignIn()
    {
        NavManager.NavigateTo("login");
    }

    public async Task Logout()
    {
        await _authStateProvider.Logout();

        NavigateToRootPage();
    }
}
