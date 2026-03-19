using Microsoft.AspNetCore.Components;

namespace Blazor.Shared.Services;

public interface INavigationService
{
    NavigationManager NavManager { get; }
    void RedirectToSignIn(); // hide this from module?
    void NavigateToRootPage(bool forceLoad = false);
    void RedirectTo(string route);
    void RedirectTo(string route, IDictionary<string, object?> queryParameters);
    Task Logout(); // hide this from module?
}
