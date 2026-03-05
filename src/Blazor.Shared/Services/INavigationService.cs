using Microsoft.AspNetCore.Components;

namespace Blazor.Shared.Services;

public interface INavigationService
{
    NavigationManager NavManager { get; }
    void RedirectToSignIn(); // hide this from module?
    void NavigateToRootPage(bool forceLoad = false);
    void RedirectTo(string uri);
    void RedirectTo(string uri, IDictionary<string, object?> queryParameters);
    Task Logout(); // hide this from module?
}
