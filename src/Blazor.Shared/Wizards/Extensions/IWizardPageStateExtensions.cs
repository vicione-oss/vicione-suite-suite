using Sdk.Client.Wizards.Services;

namespace Blazor.Shared.Wizards.Extensions;

internal static class IWizardPageStateExtensions
{
    public static bool HasLoadingOverlay(this IWizardPageState wizardPageState)
        => wizardPageState.CurrentOperation is not null;
}
