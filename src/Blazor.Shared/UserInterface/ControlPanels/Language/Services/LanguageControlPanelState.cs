using System.Globalization;
using Core.Shared.Instance.Contracts;
using Sdk.Client.ControlPanels.Services;

namespace Blazor.Shared.UserInterface.ControlPanels.Language.Services;

public sealed class LanguageControlPanelState : ControlPanelState
{
    internal static readonly CultureInfo _selectedCultureDefault = CultureInfo.DefaultThreadCurrentUICulture ?? CultureInfo.CurrentUICulture;

    public bool ShowPageRefreshInformation
    {
        get;
        set
        {
            if (value != field)
            {
                field = value;

                OnPropertyChanged();
            }
        }
    }

    public bool ShowLanguageDoesNotAffectCurrentUser
    {
        get;
        set
        {
            if (value != field)
            {
                field = value;

                OnPropertyChanged();
            }
        }
    }

    public static CultureInfo SelectedCultureDefault => _selectedCultureDefault;
    internal CultureInfo SelectedCulture { get; set; } = _selectedCultureDefault;
    internal CrossInstanceConfiguration? CrossInstanceConfiguration { get; set; }

    public bool HasChanges() => SelectedCulture.Name != CrossInstanceConfiguration?.CultureName;
}
