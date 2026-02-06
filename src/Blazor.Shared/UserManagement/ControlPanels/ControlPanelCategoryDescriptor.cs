using Blazor.Shared.Enums;
using Blazor.Shared.Extensions;
using Sdk.Client.ControlPanels.Services;
using ViciOne.Ui.Localization.Resources;

namespace Blazor.Shared.UserManagement.ControlPanels;

internal sealed class ControlPanelCategoryDescriptor : IControlPanelCategoryDescriptor
{
    public string Title => TechnicalTerms.UserManagement;
    public string? IconCssClass => null;
    public Uri? IconUrl => SvgIcon.User.GetPath();
    public int? Position => null;
}
