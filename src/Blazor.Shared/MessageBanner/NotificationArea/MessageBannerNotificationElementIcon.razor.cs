using Microsoft.AspNetCore.Components;
using Sdk.MessageBanner.Contracts;

namespace Blazor.Shared.MessageBanner.NotificationArea;

public sealed partial class MessageBannerNotificationElementIcon : ComponentBase, IDisposable
{
    [Inject]
    public required MessageBannerNotificationElementIconState State { get; set; }

    protected override void OnInitialized()
        => State.Changed += StateChanged;

    public void Dispose()
    {
        State.Changed -= StateChanged;

        GC.SuppressFinalize(this);
    }

    private void StateChanged()
        => InvokeAsync(StateHasChanged);

    private string GetColor() => State.MessageType switch
    {
        MessageType.Warning => "brightness(0) saturate(100%) invert(69%) sepia(61%) saturate(674%) hue-rotate(1deg) brightness(106%) contrast(104%)",
        MessageType.Error => "brightness(0) saturate(100%) invert(62%) sepia(62%) saturate(6885%) hue-rotate(339deg) brightness(91%) contrast(96%)",
        _ => "brightness(0) saturate(100%) invert(54%) sepia(42%) saturate(332%) hue-rotate(174deg) brightness(96%) contrast(101%)",
    };
}
