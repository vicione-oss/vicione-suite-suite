using Sdk.Messaging;

namespace Core.Shared.Persistence.Events;

[ForwardToUI]
public record RestoreBackupPrepared(bool Restart, ErrorInfo? ErrorInfo = null) : IEvent
{
    public const int RestartServiceDisabled = 101;

    /// <summary>
    /// The error message lists the HostManagement names of the disabled settings.
    /// </summary>
    public const int SettingsDisabled = 102;

    /// <summary>
    /// The backup holds a system configuration from before HostManagement 2.0.
    /// </summary>
    public const int BackupFormatNotSupported = 103;
}

