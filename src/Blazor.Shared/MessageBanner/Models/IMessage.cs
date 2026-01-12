using Blazor.Shared.Enums;
using Sdk.MessageBanner.Contracts;

namespace Blazor.Shared.MessageBanner.Models;

public interface IMessage
{
    MessageType Type { get; }
    SvgIcon Icon { get; }
    string Title { get; }
    string? Description { get; }
}
