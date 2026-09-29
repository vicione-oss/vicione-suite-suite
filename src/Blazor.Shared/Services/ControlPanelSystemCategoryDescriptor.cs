using Blazor.Shared.Enums;
using Blazor.Shared.Extensions;
using Sdk.Client.ControlPanels.Services;
using ViciOne.Ui.Localization.Resources;

namespace Blazor.Shared.Services;

internal sealed class ControlPanelSystemCategoryDescriptor : IControlPanelCategoryDescriptor
{
    public string Title => CommonVocabulary.System;
    public string? IconCssClass => null;
    public Uri? IconUrl => SvgIcon.CloudConnection.GetPath();
    public int? Position => 1;
}
