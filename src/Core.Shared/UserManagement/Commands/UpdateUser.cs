using Core.Shared.UserManagement.Contracts;
using Sdk.Messaging;

namespace Core.Shared.UserManagement.Commands;

public sealed record UpdateUser(UserProfile UserProfile, string RequestingUserName = "") : ICommand
{
    public Guid CorrelationId { get; init; } = Guid.NewGuid();
}
