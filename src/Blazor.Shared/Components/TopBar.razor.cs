using System.ComponentModel;
using Blazor.Shared.Services;
using Microsoft.AspNetCore.Components;
using Sdk.Client.Services;

namespace Blazor.Shared.Components;

public sealed partial class TopBar : ComponentBase, IDisposable
{
    [Inject] private ILayoutService LayoutService { get; set; } = default!;
    [Inject] private INavigationService NavigationService { get; set; } = default!;

    public void Dispose()
    {
        LayoutService.PropertyChanged -= OnLayoutServicePropertyChanged;
    }

    protected override void OnInitialized()
    {
        ArgumentNullException.ThrowIfNull(LayoutService, nameof(LayoutService));
        LayoutService.PropertyChanged += OnLayoutServicePropertyChanged;
    }

    private async void OnLayoutServicePropertyChanged(object? _, PropertyChangedEventArgs e)
    {
        await InvokeAsync(StateHasChanged);
    }

    private void NavigateToRootPage()
        => NavigationService.NavigateToRootPage();
}
