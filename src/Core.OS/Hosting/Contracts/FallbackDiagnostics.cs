namespace Core.OS.Hosting.Contracts;

/// <summary>
/// Everything the failsafe debug page renders, resolved before rendering starts. The page reads
/// no files: the failsafe host runs because configuration is broken, and an exception during
/// rendering would answer with a bare 500 - under <c>Restart=always</c> a page nobody ever sees.
/// A group that could not be resolved is <see langword="null"/> and rendered as unavailable.
/// Public because a Razor component's generated class is public, and it takes this as a parameter.
/// </summary>
public sealed record FallbackDiagnostics
{
    public required string Status { get; init; }

    public required IReadOnlyList<string> Messages { get; init; }

    public required DateTimeOffset Timestamp { get; init; }

    public required FallbackVersionDiagnostics Version { get; init; }

    public FallbackRecoveryDiagnostics? Recovery { get; init; }

    public required FallbackEnvironmentOverridesDiagnostics EnvironmentOverrides { get; init; }
}
