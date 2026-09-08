namespace Core.OS.Hosting.Contracts;

public sealed record FallbackRecoveryDiagnostics
{
    public required DateTimeOffset LastStartup { get; init; }

    public required int Startups { get; init; }

    public required bool RecoveryApplied { get; init; }
}
