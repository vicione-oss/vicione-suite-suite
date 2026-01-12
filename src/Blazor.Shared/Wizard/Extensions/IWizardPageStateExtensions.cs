using Blazor.Shared.Wizard.Services;

namespace Blazor.Shared.Wizard.Extensions;

internal static class IWizardPageStateExtensions
{
    public static bool HasLoadingOverlay(this IWizardPageState wizardPageState)
        => wizardPageState.CurrentOperation is not null;
}
