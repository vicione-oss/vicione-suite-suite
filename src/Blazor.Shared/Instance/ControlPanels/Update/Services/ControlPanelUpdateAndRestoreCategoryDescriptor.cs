using System.Globalization;
using Sdk.Client.ControlPanels.Services;
using ViciOne.Ui.Localization.Resources;
using ViciOne.Ui.MonochromeIcons.Core.Enums;
using ViciOne.Ui.MonochromeIcons.Core.Extensions;

namespace Blazor.Shared.Instance.ControlPanels.Update.Services;

internal sealed class ControlPanelUpdateAndRestoreCategoryDescriptor : IControlPanelCategoryDescriptor
{
    public string Title => string.Format(CultureInfo.CurrentCulture, CommonPatterns.ThisAndThat, TechnicalTerms.Update, TechnicalTerms.Restore);
    public string IconCssClass => MonochromeIconName.Refresh.GetCssClasses(MonochromeIconSize.SmallMedium).ToSpaceSeparated();
    public Uri? IconUrl => null;
    public int? Position => null;
}
