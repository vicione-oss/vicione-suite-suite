using Burger.Client.Contracts;
using Burger.Client.Services;
using Microsoft.AspNetCore.Components;

namespace Burger.Client.Pages;

public sealed partial class BurgerPage
{
    [Inject] private OrderBurgerService BurgerService { get; set; } = default!;

    private BurgerViewModel Burger { get; } = new();

    protected override void OnAfterInitialized() => BurgerService.PropertyChanged += OnPropertyChanged;

    private async void OnPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        => await InvokeAsync(StateHasChanged);

    private async Task SubmitBurgerOrder() => await BurgerService.OrderBurger(Burger);

    protected override ValueTask DisposeInternal()
    {
        BurgerService.PropertyChanged -= OnPropertyChanged;
        return base.DisposeInternal();
    }
}
