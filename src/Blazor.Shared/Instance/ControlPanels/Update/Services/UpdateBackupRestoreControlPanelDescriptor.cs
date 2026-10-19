using Sdk.Client.ControlPanels.Services;
using ViciOne.Ui.Localization.Resources;

namespace Blazor.Shared.Instance.ControlPanels.Update.Services;

internal sealed class UpdateBackupRestoreControlPanelDescriptor : IControlPanelDescriptor<UpdateBackupRestoreControlPanel>
{
    public string Title => string.Format(System.Globalization.CultureInfo.CurrentCulture,
        CommonPatterns.ThisAndThat, TechnicalTerms.Backup, TechnicalTerms.Restore);

    public Uri? IconUrl => null;
    public int? Position => 1;
    public bool ShowInNavigation => false;
}
