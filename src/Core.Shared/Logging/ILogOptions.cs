namespace Core.Shared.Logging;

[System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1819: Properties should not return arrays", Justification = "DTO")]
public interface ILogOptions
{
    string? LogPath { get; }

    string[]? LogTargets { get; }

    string? LogFileNameTemplate { get; }
}
