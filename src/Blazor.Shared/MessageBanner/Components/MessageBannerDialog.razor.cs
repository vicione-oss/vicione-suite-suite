using Blazor.Shared.MessageBanner.Services;
using Microsoft.AspNetCore.Components;

namespace Blazor.Shared.MessageBanner.Components;

public sealed partial class MessageBannerDialog : IDisposable
{
    [Inject] public required MessageBannerDialogState State { get; set; }

    [Inject] public required IMessageBannerMediator MessageBannerMediator { get; set; }

    protected override void OnInitialized()
        => State.Changed += StateChanged;

    public void Dispose()
    {
        State.Changed -= StateChanged;

        GC.SuppressFinalize(this);
    }

    private void StateChanged()
        => InvokeAsync(StateHasChanged);

    private void MinimizeButtonClick()
        => MessageBannerMediator.MinimizeMessageBanner();
}
