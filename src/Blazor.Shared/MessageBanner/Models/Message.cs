using Sdk.MessageBanner.Contracts;
using ViciOne.Ui.MonochromeIcons.Core.Enums;

namespace Blazor.Shared.MessageBanner.Models;

internal sealed class Message : IMessage
{
    public MessageType Type { get; set; }
    public MonochromeIconName Icon { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
}
