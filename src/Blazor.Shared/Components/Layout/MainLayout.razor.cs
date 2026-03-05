using System.ComponentModel;
using Blazor.Shared.Module.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Sdk.Client.Services;

namespace Blazor.Shared.Components.Layout;

public sealed partial class MainLayout : IDisposable
{
    private bool _isLogin;

    [Inject] private ILayoutService LayoutService { get; set; } = default!;
    [Inject] private IClientModuleService ModuleService { get; set; } = default!;
    [Inject] private IServiceProvider ServiceProvider { get; set; } = default!;
    [Inject] private NavigationManager NavigationManager { get; set; } = default!;
    [CascadingParameter] private Task<AuthenticationState>? ExistingCascadedAuthenticationState { get; set; }

    public void Dispose()
    {
        LayoutService.PropertyChanged -= LayoutService_PropertyChanged;
    }

    private void LayoutService_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        => InvokeAsync(StateHasChanged);

    protected override void OnInitialized()
    {
        LayoutService.PropertyChanged += LayoutService_PropertyChanged;
    }

    protected override async Task OnInitializedAsync()
    {
        if (ExistingCascadedAuthenticationState is not null)
        {
            await ModuleService.OnUserAuthenticated(ServiceProvider, (await ExistingCascadedAuthenticationState).User);

            _isLogin = NavigationManager.Uri.Contains("account/", StringComparison.OrdinalIgnoreCase);
        }
    }

    private static void OnPrepareRecover(Exception error)
    {
        // We now can prepare the recovery but we don't know what the loaded module was doing to provoke the issue
        // so a simple restore can be enough but could also fail again and again.
        // The exception will be logged by the ErrorBoundary component.
    }
}
