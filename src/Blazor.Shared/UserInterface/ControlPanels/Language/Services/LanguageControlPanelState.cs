using System.Globalization;
using Core.Shared.Instance.Contracts;
using Sdk.Client.ControlPanels.Services;
using ViciOne.SourceGenerators.NotifyChanged;

namespace Blazor.Shared.UserInterface.ControlPanels.Language.Services;

public sealed partial class LanguageControlPanelState : ControlPanelState
{
    internal static readonly CultureInfo _selectedCultureDefault = CultureInfo.DefaultThreadCurrentUICulture ?? CultureInfo.CurrentUICulture;

    [NotifyChanged]
    public partial bool ShowPageRefreshInformation { get; set; }

    [NotifyChanged]
    public partial bool ShowLanguageDoesNotAffectCurrentUser { get; set; }

    public static CultureInfo SelectedCultureDefault => _selectedCultureDefault;
    internal CultureInfo SelectedCulture { get; set; } = _selectedCultureDefault;
    internal CrossInstanceConfiguration? CrossInstanceConfiguration { get; set; }

    public bool HasChanges() => SelectedCulture.Name != CrossInstanceConfiguration?.CultureName;
}
