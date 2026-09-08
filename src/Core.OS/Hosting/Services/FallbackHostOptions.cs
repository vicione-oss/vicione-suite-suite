using System.IO.Abstractions;
using Core.OS.Instance;
using Core.Shared.HostManagement;

namespace Core.OS.Hosting.Services;

internal sealed class FallbackHostOptions
{
    public required string Status { get; init; }
    public required string[] Messages { get; init; }
    public int HttpStatusCode { get; init; } = 503;
    public LogLevel LogLevel { get; init; } = LogLevel.Error;

    /// <summary>
    /// Where the failsafe host reports to. Handed in rather than resolved from the host's own
    /// container: that container has no Serilog, so a logger taken from it would write past the
    /// configured sinks - and on a crash-looping device those carry the one log that matters.
    /// </summary>
    public required ILogger Logger { get; init; }

    /// <summary>
    /// The context the debug surface reads its diagnostic groups from. Optional rather than
    /// required: the failsafe host runs because startup broke, so a caller that cannot supply it
    /// must still get a page. The groups that depend on it then render as unavailable.
    /// </summary>
    public IFileSystem? FileSystem { get; init; }
    public InstanceOptions? Instance { get; init; }
    public HostManagementOptions? HostManagement { get; init; }

    /// <summary>
    /// How long the operator action waits before requesting the restart, so its redirect reaches
    /// the browser first.
    /// </summary>
    public int StopApplicationDelayMs { get; init; } = 2000;
}
