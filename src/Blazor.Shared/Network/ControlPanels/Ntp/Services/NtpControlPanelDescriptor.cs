using Blazor.Shared.Network.ControlPanels.Ntp.Components;
using Blazor.Shared.Network.Services;
using Sdk.Client.ControlPanels.Services;
using ViciOne.Ui.Localization.Resources;

namespace Blazor.Shared.Network.ControlPanels.Ntp.Services;

internal sealed class NtpControlPanelDescriptor : IControlPanelDescriptor<NtpControlPanel>
{
    public string Title => TechnicalAcronyms.Ntp;
    public int? Position => ControlPanelNetworkCategoryDescriptor.FirstPositionAfterNetworkInterfaces + 1;
    public Uri? IconUrl => null;
}
