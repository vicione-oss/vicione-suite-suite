using Bunit;
using DevExpress.Blazor.Internal;

namespace Blazor.Tests.Tools;

public static class BunitJSInteropExtensions
{
    extension(BunitJSInterop interop)
    {
        /// <summary>
        /// Possible workaround for bUnit integration with DevExpress Blazor controls provided at
        /// https://supportcenter.devexpress.com/ticket/details/t1056787/devexpress-and-bunit-support
        /// </summary>
        public BunitJSInterop ConfigureJSInteropForDx()
        {
            interop.Mode = JSRuntimeMode.Loose;

            var rootModule = interop.SetupModule("./_content/DevExpress.Blazor/dx-blazor.js");
            rootModule.Mode = JSRuntimeMode.Strict;
            rootModule.Setup<DeviceInfo>("getDeviceInfo", _ => true).SetResult(new DeviceInfo(false));

            return interop;
        }
        /// <summary>
        /// Possible workaround for bUnit integration with DevExpress Blazor controls provided at
        /// https://supportcenter.devexpress.com/ticket/details/t1056787/devexpress-and-bunit-support
        /// </summary>
        public BunitJSInterop ConfigureJSInteropForResizeObserver()
        {
            interop.Mode = JSRuntimeMode.Loose;

            var rootModule = interop.SetupModule("./_content/ViciOne.Ui.Blazor.Components/resizing/resize-observer.js");
            rootModule.Mode = JSRuntimeMode.Loose;

            return interop;
        }
        

        /// <summary>
        /// Possible workaround for bUnit integration with DevExpress Blazor controls provided at
        /// https://supportcenter.devexpress.com/ticket/details/t1056787/devexpress-and-bunit-support
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
