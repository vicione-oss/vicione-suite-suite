using Blazor.Shared.Enums;
using Blazor.Shared.MessageBanner.Models;
using Sdk.MessageBanner.Contracts;

namespace Blazor.Shared.Tests.MessageBanner;

internal sealed class DummyMessage : IMessage
{
    public MessageType Type => MessageType.Information;

    public SvgIcon Icon => SvgIcon.CloudConnection;

    public string Title => "Dummy title";

    public string? Description => "Dummy description";
}
