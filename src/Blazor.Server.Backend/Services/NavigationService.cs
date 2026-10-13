using Blazor.Shared.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;
using Sdk.Client.NotificationArea.Services;
using Sdk.Client.Services;

namespace Blazor.Server.Backend.Services;

internal sealed partial class NavigationService : INavigationService, IDisposable
{
    private readonly ILayoutService _layoutService;
    private readonly IActiveNotificationElementPolicy _activeNotificationElementPolicy;
    private readonly IJSRuntime _jsRuntime;
    private readonly ILogger<NavigationService> _logger;

    public NavigationManager NavManager { get; }

    public NavigationService(
        NavigationManager navigationManager,
        ILayoutService layoutService,
        IActiveNotificationElementPolicy activeNotificationElementPolicy,
        IJSRuntime jsRuntime,
        ILogger<NavigationService> logger)
    {
        NavManager = navigationManager;
        NavManager.LocationChanged += NavigationManager_LocationChanged;
        _layoutService = layoutService;
        _activeNotificationElementPolicy = activeNotificationElementPolicy;
        _jsRuntime = jsRuntime;
        _logger = logger;
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

        return EndCircuitAndNavigate("ViciOne.Interop.signOut", uriBuilder.Uri.AbsoluteUri);
    }

    public void RedirectToSignIn()
    {
        var uriBuilder = new UriBuilder($"{NavManager.BaseUri}account/login");

        _ = EndCircuitAndNavigate("ViciOne.Interop.redirectToSignIn", uriBuilder.Uri.AbsoluteUri);
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

    private async Task EndCircuitAndNavigate(string identifier, string uri)
    {
        try
        {
            await _jsRuntime.InvokeVoidAsync(identifier, uri);
        }
        catch (Exception ex) when (ex is OperationCanceledException or JSDisconnectedException)
        {
            // Ending the circuit cancels this in-flight interop call.
            LogNavigationCancelled(_logger, ex, identifier);
        }
        catch (Exception ex)
        {
            // Callers are event handlers and a fire-and-forget redirect, so an exception would fail the circuit or be lost.
            LogNavigationFailed(_logger, ex, identifier);
        }
    }

    private void NoActiveNotificationElement()
        => _activeNotificationElementPolicy.NoneActive();

    [LoggerMessage(Level = LogLevel.Debug, Message = "Navigation via '{Identifier}' was cancelled by the ending circuit.")]
    private static partial void LogNavigationCancelled(ILogger logger, Exception exception, string identifier);

    [LoggerMessage(Level = LogLevel.Error, Message = "Navigation via '{Identifier}' failed.")]
    private static partial void LogNavigationFailed(ILogger logger, Exception exception, string identifier);
}
