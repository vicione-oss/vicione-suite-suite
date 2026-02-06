using System.Globalization;
using Blazor.Shared.UserInterface.ControlPanels.DateAndTime.Components;
using Sdk.Client.ControlPanels.Services;
using ViciOne.Ui.Localization.Resources;

namespace Blazor.Shared.UserInterface.ControlPanels.DateAndTime.Services;

internal sealed class DateAndTimeControlPanelDescriptor : IControlPanelDescriptor<DateAndTimeControlPanel>
{
    public string Title => string.Format(CultureInfo.CurrentCulture, CommonPatterns.ThisAndThat, CommonVocabulary.Date, CommonVocabulary.Time);
    public Uri? IconUrl => null;
    public int? Position => null;
}
