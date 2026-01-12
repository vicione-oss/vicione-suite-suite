using Sdk.Messaging;

namespace Core.Shared.Instance.Commands;

public sealed record SetCrossInstanceConfiguration(string? CultureName, string? TimeZoneId) : ICommand
{
    public Guid CorrelationId { get; set; } = Guid.NewGuid();
}
