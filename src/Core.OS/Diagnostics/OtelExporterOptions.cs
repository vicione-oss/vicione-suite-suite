namespace Core.OS.Diagnostics;

/// <summary>
/// The standard OTLP exporter variables. They are read through
/// <see cref="IConfiguration"/> instead of the raw process environment, so values
/// from other configuration providers (e.g., instance configuration distributed by
/// the master) are honored too.
/// </summary>
internal sealed class OtelExporterOptions
{
    [ConfigurationKeyName(OtelEnvironment.Endpoint)]
    public string? Endpoint { get; init; }

    [ConfigurationKeyName(OtelEnvironment.Protocol)]
    public string? Protocol { get; init; }

    [ConfigurationKeyName(OtelEnvironment.ServiceName)]
    public string? ServiceName { get; init; }

    [ConfigurationKeyName(OtelEnvironment.AdditionalMeters)]
    public string[] AdditionalMeters { get; init; } = [];

    [ConfigurationKeyName(OtelEnvironment.AdditionalSources)]
    public string[] AdditionalSources { get; init; } = [];
}
