namespace Core.OS.Diagnostics.Extensions;

internal static class IConfigurationExtensions
{
    /// <summary>
    /// Unlike the suite's own options, the OTLP variables are not a configuration
    /// section but flat, spec-defined keys, so they are bound off the configuration
    /// root. Absent keys are not a misconfiguration - OpenTelemetry is off by default.
    /// </summary>
    internal static OtelExporterOptions GetOtelExporterOptions(this IConfiguration config)
        => config.Get<OtelExporterOptions>() ?? new OtelExporterOptions();
}
