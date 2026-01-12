using Sdk.Messaging;

namespace Core.Shared.Security;

public record SendVerifyEmailAddressLink(string UserId, string CallbackLink) : ICommand
{
    public Guid CorrelationId { get; init; } = Guid.NewGuid();
}
