using Blazor.Shared.Settings.Models;

namespace Blazor.Shared.Settings.Extensions;

internal static partial class IControlPanelEditExtensions
{
    public static async Task<bool> Cancel(this IControlPanelEdit controlPanelEdit, bool withReset)
    {
        var result = await controlPanelEdit.Cancel();

        if (result && withReset)
            result = await controlPanelEdit.Reset();

        return result;
    }
}
