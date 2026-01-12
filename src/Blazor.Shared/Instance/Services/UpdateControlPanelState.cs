using Blazor.Shared.Settings.Models;
using Sdk.Client.ControlPanels.Services;
using Sdk.Instance;
using ViciOne.Ui.Blazor.Components.ComboBox;

namespace Blazor.Shared.Instance.Services;

public sealed class UpdateControlPanelState(IInstanceInformationProvider instanceInformationProvider) : ControlPanelState
{
    internal List<ComboBoxItem<string?, string>>? VersionComboBoxItems { get; set; }

    internal string? FetchAvailableVersionsError { get; set; }

    internal bool FetchingVersions { get; set; }

    internal string? SelectedVersion { get; set; }
    internal bool SelectedVersionChanged { get; set; }
    internal bool BackupGroupExpanded { get; set; } = true;
    internal bool RestoreGroupExpanded { get; set; } = true;

    internal bool FlashDeviceEnabled { get; set; }
    internal string? SwuFilename { get; set; }
    internal IUploadTicket? SwuFileUploadTicket { get; set; }
    internal string? SwuFilenameUploaded { get; set; }
    internal bool SwuFilenameChangedBannerVisible { get; set; }

    internal void PreselectCurrentVersion()
    {
        SelectedVersion = GetCurrentVersion();

        SelectedVersionChanged = false;
    }

    internal string GetCurrentVersion()
        => instanceInformationProvider.Local.Version;
}
