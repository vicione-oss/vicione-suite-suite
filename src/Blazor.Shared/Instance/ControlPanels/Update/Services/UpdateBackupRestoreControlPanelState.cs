using Sdk.Client.ControlPanels.Services;
using Sdk.Client.Models;

namespace Blazor.Shared.Instance.ControlPanels.Update.Services;

public sealed class UpdateBackupRestoreControlPanelState : ControlPanelState
{
    internal bool BackupGroupExpanded { get; set; } = true;

    internal string? RestoreFilename { get; set; }
    internal IUploadTicket? RestoreFileUploadTicket { get; set; }
    internal bool RestoreUploadRunning { get; set; }
}
