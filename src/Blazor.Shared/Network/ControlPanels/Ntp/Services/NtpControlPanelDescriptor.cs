using Blazor.Shared.Network.ControlPanels.Ntp.Components;
using Sdk.Client.ControlPanels.Services;
using ViciOne.Ui.Localization.Resources;

namespace Blazor.Shared.Network.ControlPanels.Ntp.Services;

internal sealed class NtpControlPanelDescriptor : IControlPanelDescriptor<NtpControlPanel>
{
    public string Category => CommonVocabulary.Network;
    public string Title => TechnicalAcronyms.Ntp;
    public Uri? IconUrl => null;
}
