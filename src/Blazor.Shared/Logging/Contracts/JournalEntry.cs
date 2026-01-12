namespace Blazor.Shared.Logging.Contracts;

public record JournalEntry(DateTimeOffset Timestamp, string Hostname, string Unit, int? PID, int Priority, string Message);
