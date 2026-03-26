using Blazor.Shared.MessageBanner.Models;
using Sdk.MessageBanner.Contracts;
using ViciOne.Ui.MonochromeIcons.Core.Enums;

namespace Blazor.Shared.Tests.MessageBanner;

internal sealed class DummyMessage : IMessage
{
    public MessageType Type => MessageType.Information;

    public MonochromeIconName Icon => MonochromeIconName.InfoLight;

    public string Title => "Dummy title";

    public string? Description => "Dummy description";
}
