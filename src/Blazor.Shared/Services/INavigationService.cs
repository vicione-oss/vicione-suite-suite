using Microsoft.AspNetCore.Components;

namespace Blazor.Shared.Services;

public interface INavigationService
{
    NavigationManager NavManager { get; }
    void RedirectToSignIn(); // hide this from module?
    void NavigateToRootPage(bool forceLoad = false);

    /// <summary>
    /// Redirects the user to the specified relative route within the application.
    ///
    /// This might throw <see cref="NavigationException"/> in SSR, which is needed by the framework.
    /// We must not catch those!!!
    /// See https://github.com/dotnet/aspnetcore/issues/28355
    /// </summary>
    /// <param name="route">The relative URI to navigate to. If the provided route is null or invalid,
    /// it defaults to the base relative path of the application.</param>
    void RedirectTo(string route);
    void RedirectTo(string route, IDictionary<string, object?> queryParameters);
    Task Logout(); // hide this from module?
}
