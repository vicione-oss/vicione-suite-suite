using Sdk.MessageBanner.Contracts;
using ViciOne.Ui.MonochromeIcons.Core.Enums;

namespace Blazor.Shared.MessageBanner.Models;

public interface IMessage
{
    MessageType Type { get; }
    MonochromeIconName Icon { get; }
    string Title { get; }
    string? Description { get; }
}
