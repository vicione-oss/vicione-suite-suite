using Blazor.Shared.Settings.Models;
using Core.Shared.HostManagement;
using Sdk.Client.ControlPanels.Services;
using ViciOne.Ui.Blazor.Components.ComboBox;

namespace Blazor.Shared.Instance.ControlPanels.Update.Services;

public sealed class UpdateControlPanelState() : ControlPanelState
{
    internal List<SuiteVersionPackage>? SuiteVersions { get; set; }

    internal List<ComboBoxItem<string, string>>? VersionComboBoxItems =>
        SuiteVersions?.Select(v => new ComboBoxItem<string, string>
        {
            Value = v.Version,
            Text = v.Version
        })?.ToList();

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
        SelectedVersion = SuiteVersions?
            .FirstOrDefault(k => k.Installed)?
            .Version;

        SelectedVersionChanged = false;
    }
}
