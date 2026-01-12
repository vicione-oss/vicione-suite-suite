using Microsoft.AspNetCore.Components;

namespace Blazor.Shared.Services;

public interface INavigationService
{
    NavigationManager NavManager { get; }
    void RedirectToSignIn(); // hide this from module?
    void NavigateToRootPage(bool forceLoad = false);
    Task Logout(); // hide this from module?
}
