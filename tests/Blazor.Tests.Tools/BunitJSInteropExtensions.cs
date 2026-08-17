using Bunit;
using GridComponent = ViciOne.Ui.Blazor.Components.Grid.Components.Grid<object>;

namespace Blazor.Tests.Tools;

public static class BunitJSInteropExtensions
{
    extension(BunitJSInterop interop)
    {
        /// <summary>
        /// Configures a handler for JS-interop calls associated with <see cref="GridComponent"/> using loose mode.
        /// </summary>
        public BunitJSInterop ConfigureQuickGridJSInterop()
        {
            interop.Mode = JSRuntimeMode.Loose;

            var rootModule = interop.SetupModule("./_content/Microsoft.AspNetCore.Components.QuickGrid/QuickGrid.razor.js");
            rootModule.Mode = JSRuntimeMode.Loose;

            return interop;
        }
    }
}
