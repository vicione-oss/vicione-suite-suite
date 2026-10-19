using Blazor.Shared.Enums;
using Blazor.Shared.Extensions;
using Blazor.Shared.Network.ControlPanels.Proxies.Components;
using Blazor.Shared.Network.Services;
using Sdk.Client.ControlPanels.Services;
using ViciOne.Ui.Localization.Resources;

namespace Blazor.Shared.Network.ControlPanels.Proxies.Services;

internal sealed class ProxiesControlPanelDescriptor : IControlPanelDescriptor<ProxiesControlPanel>
{
    public string Category => CommonVocabulary.Network;
    public string Title => TechnicalTerms.ProxyPlural;
    public int? Position => ControlPanelNetworkCategoryDescriptor.FirstPositionAfterNetworkInterfaces + 2;
    public Uri? IconUrl => SvgIcon.HostConfig.GetPath();
}
