using Blazor.Shared.Enums;
using Sdk.MessageBanner.Contracts;

namespace Blazor.Shared.MessageBanner.Models;

internal sealed class Message : IMessage
{
    public MessageType Type { get; set; }
    public SvgIcon Icon { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
}
