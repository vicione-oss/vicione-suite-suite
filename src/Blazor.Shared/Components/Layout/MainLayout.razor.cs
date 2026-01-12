using System.ComponentModel;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Sdk.Client.Services;

namespace Blazor.Shared.Components.Layout;

public sealed partial class MainLayout : IDisposable
{
    private bool _isTemplateLogin;

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

            _isTemplateLogin = NavigationManager.Uri.Contains("template/", StringComparison.OrdinalIgnoreCase);
        }
    }
}
