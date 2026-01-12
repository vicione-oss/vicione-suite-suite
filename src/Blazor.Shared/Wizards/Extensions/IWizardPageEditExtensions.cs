using Blazor.Shared.Wizards.Models;

namespace Blazor.Shared.Wizards.Extensions;

internal static partial class IWizardPageEditExtensions
{
    public static async Task<bool> Cancel(this IWizardPageEdit wizardPageEdit, bool withReset)
    {
        var result = await wizardPageEdit.Cancel();

        if (result && withReset)
            result = await wizardPageEdit.Reset();

        return result;
    }
}
