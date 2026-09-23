using Blazor.Shared.Services;
using Microsoft.AspNetCore.Components;
using Sdk.Client.NotificationArea.Services;
using Sdk.Client.Services;

namespace Blazor.Server.Backend.Services;

internal sealed class NavigationService : INavigationService, IDisposable
{
    private readonly ILayoutService _layoutService;
    private readonly IActiveNotificationElementPolicy _activeNotificationElementPolicy;
    private readonly IJsInterop _interop;

    public NavigationManager NavManager { get; }

    public NavigationService(
        NavigationManager navigationManager,
        ILayoutService layoutService,
        IActiveNotificationElementPolicy activeNotificationElementPolicy,
        IJsInterop interop)
    {
        NavManager = navigationManager;
        NavManager.LocationChanged += NavigationManager_LocationChanged;
        _layoutService = layoutService;
        _activeNotificationElementPolicy = activeNotificationElementPolicy;
        _interop = interop;
    }

    public void Dispose() => NavManager.LocationChanged -= NavigationManager_LocationChanged;

    public void NavigateToRootPage(bool forceLoad = false) => NavManager.NavigateTo(NavManager.BaseUri, forceLoad);

    private void NavigationManager_LocationChanged(object? sender, Microsoft.AspNetCore.Components.Routing.LocationChangedEventArgs e)
    {
        if (e.Location == NavManager.BaseUri)
        {
            _layoutService.TitleBarAppName = string.Empty;
            _layoutService.IsLoadingOverlayVisible = false;

            NoActiveNotificationElement();
        }
    }

    public Task Logout()
    {
        var uriBuilder = new UriBuilder($"{NavManager.BaseUri}account/logout");

        NoActiveNotificationElement();

        return _interop.SubmitForm(uriBuilder.Uri.AbsoluteUri, "");
    }

    public void RedirectToSignIn()
    {
        var uriBuilder = new UriBuilder($"{NavManager.BaseUri}account/login");

        // Routes renders with prerender:false, so this runs in the circuit and NavigateTo goes through JS
        // rather than throwing NavigationException. The 404 in aspnetcore#18849 no longer applies: since
        // .NET 8 an unmatched URI falls back to a real browser navigation, which reaches the Razor Page.
        NavManager.NavigateTo(uriBuilder.Uri.ToString());
    }

    public void RedirectTo(string route)
    {
        route ??= "";

        // Prevent open redirects.
        if (!Uri.IsWellFormedUriString(route, UriKind.Relative))
            route = NavManager.ToBaseRelativePath(route);

        NavManager.NavigateTo(route);
    }

    public void RedirectTo(string route, IDictionary<string, object?> queryParameters)
    {
        var uriWithoutQuery = NavManager.ToAbsoluteUri(route).GetLeftPart(UriPartial.Path);
        var newUri = NavManager.GetUriWithQueryParameters(uriWithoutQuery, (IReadOnlyDictionary<string, object?>)queryParameters);
        RedirectTo(newUri);
    }

    private void NoActiveNotificationElement()
        => _activeNotificationElementPolicy.NoneActive();
}
