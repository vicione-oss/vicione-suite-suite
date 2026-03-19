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

        return _interop.SubmitForm(uriBuilder.Uri.ToString(), "");
    }

    public void RedirectToSignIn()
    {
        // issues with NavigationManager redirecting between blazor and razor (cshtml)
        // https://github.com/dotnet/aspnetcore/issues/18849
        var uriBuilder = new UriBuilder($"{NavManager.BaseUri}account/login");

        // now if we call this sync or return the Task.Completed it does not work like said in
        // the issue link. if we do it that way it is working but why?
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
