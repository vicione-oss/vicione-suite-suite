using Blazor.Shared.Enums;
using Blazor.Shared.Extensions;
using Sdk.Client.ControlPanels.Services;
using ViciOne.Ui.Localization.Resources;

namespace Blazor.Shared.Network.Services;

internal sealed class ControlPanelNetworkCategoryDescriptor : IControlPanelNetworkCategoryDescriptor
{
    /// <summary>
    /// Network interfaces up to this count keep their own order; further ones share positions with the other network
    /// control panels and fall back to sorting by title.
    /// </summary>
    public const int MaxNetworkInterfaceCount = 20;

    /// <summary>
    /// Network interfaces take their index as position, so the other network control panels start here to follow them.
    /// </summary>
    public const int FirstPositionAfterNetworkInterfaces = MaxNetworkInterfaceCount;

    public string Title => CommonVocabulary.Network;
    public string? IconCssClass => null;
    public Uri? IconUrl => SvgIcon.HostConfig.GetPath();
    public int? Position => 2;
}
