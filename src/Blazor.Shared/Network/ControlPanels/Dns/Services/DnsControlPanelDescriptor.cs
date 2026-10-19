using Blazor.Shared.Enums;
using Blazor.Shared.Extensions;
using Blazor.Shared.Network.ControlPanels.Dns.Components;
using Blazor.Shared.Network.Services;
using Sdk.Client.ControlPanels.Services;
using ViciOne.Ui.Localization.Resources;

namespace Blazor.Shared.Network.ControlPanels.Dns.Services;

internal sealed class DnsControlPanelDescriptor : IControlPanelDescriptor<DnsControlPanel>
{
    public string Category => CommonVocabulary.Network;
    public string Title => TechnicalAcronyms.Dns;
    public int? Position => ControlPanelNetworkCategoryDescriptor.FirstPositionAfterNetworkInterfaces;
    public Uri? IconUrl => SvgIcon.HostConfig.GetPath();
}
