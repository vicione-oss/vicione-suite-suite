using System.Diagnostics.CodeAnalysis;

namespace Core.OS.UserManagement.Entities;

public class UserTicket
{
    public Guid Id { get; set; }

    public required string UserId { get; set; }

    [SuppressMessage("Performance", "CA1819:Properties should not return arrays")]
    public required byte[] Value { get; set; }

    public DateTimeOffset? LastActivity { get; set; }

    public DateTimeOffset? Expires { get; set; }
}
