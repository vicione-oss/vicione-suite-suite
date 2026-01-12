namespace Core.Shared.Persistence.Contracts;

public record BackupFileInfo(string Filename, DateTimeOffset Timestamp, long SizeInBytes);
