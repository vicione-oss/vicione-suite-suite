using Blazor.Shared.Enums;
using Blazor.Shared.Extensions;
using Blazor.Shared.Network.ControlPanels.Proxies.Components;
using Sdk.Client.ControlPanels.Services;
using ViciOne.Ui.Localization.Resources;

namespace Blazor.Shared.Network.ControlPanels.Proxies.Services;

internal sealed class ProxiesControlPanelDescriptor : IControlPanelDescriptor<ProxiesControlPanel>
{
    public string Category => CommonVocabulary.Network;
    public string Title => TechnicalTerms.ProxyPlural;
    public string IconPath => SvgIcon.HostConfig.GetPath();
}
