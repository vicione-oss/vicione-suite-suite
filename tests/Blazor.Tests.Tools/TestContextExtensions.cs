using Bunit;
using Sdk.Testing.Client;
using ViciOne.Ui.Blazor.Components.CheckBox.Extensions;
using ViciOne.Ui.Blazor.Components.SpinEdit.Extensions;
using ViciOne.Ui.Blazor.Components.TestingHelpers.Resizing.Extensions;
using ViciOne.Ui.Blazor.Components.Toolbar.Extensions;

namespace Blazor.Tests.Tools;

public static class TestContextExtensions
{
    extension(BunitContext ctx)
    {
        public BunitContext SetupBlazorUiComponents(Action<ClientServiceConfigurator>? setup = null)
        {
            ctx.SetupSuiteServices(setup);

            ctx.JSInterop.Mode = JSRuntimeMode.Loose;
            ctx.JSInterop.SetupForResizeObserver();

            ctx.Services
                .AddCheckBox()
                .AddShortSpinEdit()
                .AddIntSpinEdit()
                .AddFloatSpinEdit()
                .AddToolbar();

            return ctx;
        }
    }
}
