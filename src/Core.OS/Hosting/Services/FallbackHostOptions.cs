namespace Core.OS.Hosting.Services;

internal sealed class FallbackHostOptions
{
    public required string Status { get; init; }
    public required string[] Messages { get; init; }
    public int HttpStatusCode { get; init; } = 503;
    public Serilog.Events.LogEventLevel LogLevel { get; init; } = Serilog.Events.LogEventLevel.Error;
}
