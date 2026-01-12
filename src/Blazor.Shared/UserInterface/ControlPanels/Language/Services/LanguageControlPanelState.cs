using System.Globalization;
using Core.Shared.Instance.Contracts;
using Sdk.Client.ControlPanels.Services;

namespace Blazor.Shared.UserInterface.ControlPanels.Language.Services;

public sealed class LanguageControlPanelState : ControlPanelState
{
    internal static readonly CultureInfo _selectedCultureDefault = CultureInfo.DefaultThreadCurrentUICulture ?? CultureInfo.CurrentUICulture;

    private bool _showLanguageSavedBanner;

    public bool ShowLanguageSavedBanner
    {
        get => _showLanguageSavedBanner;
        set
        {
            if (value != _showLanguageSavedBanner)
            {
                _showLanguageSavedBanner = value;

                OnPropertyChanged();
            }
        }
    }

    public static CultureInfo SelectedCultureDefault => _selectedCultureDefault;

    internal bool SelectedCultureLoading { get; set; } = true;
    internal CultureInfo SelectedCulture { get; set; } = _selectedCultureDefault;
    internal CrossInstanceConfiguration? CrossInstanceConfiguration { get; set; }
}
