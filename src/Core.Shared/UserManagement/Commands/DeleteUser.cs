using Core.Shared.UserManagement.Contracts;
using Sdk.Messaging;

namespace Core.Shared.UserManagement.Commands;

public sealed record DeleteUser(UserProfile UserProfile) : ICommand
{
    public Guid CorrelationId { get; set; } = Guid.NewGuid();
}
