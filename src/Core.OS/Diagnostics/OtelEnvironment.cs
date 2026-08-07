namespace Core.OS.Diagnostics;

internal static class OtelEnvironment
{
    public const string Endpoint = "OTEL_EXPORTER_OTLP_ENDPOINT";
    public const string Protocol = "OTEL_EXPORTER_OTLP_PROTOCOL";
    public const string ServiceName = "OTEL_SERVICE_NAME";
    public const string AdditionalMeters = "OTEL_ADDITIONAL_METERS";
    public const string AdditionalSources = "OTEL_ADDITIONAL_SOURCES";
}
