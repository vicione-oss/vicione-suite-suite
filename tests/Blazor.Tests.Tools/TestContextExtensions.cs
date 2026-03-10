using Bunit;
using DevExpress.Blazor.Internal;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Sdk.Testing.Client;
using ViciOne.Ui.Blazor.Components.CheckBox.Extensions;
using ViciOne.Ui.Blazor.Components.SpinEdit.Extensions;
using ViciOne.Ui.Blazor.Components.Toolbar.Extensions;
using ViciOne.Ui.Shared.Dx.Components.Resizing;

namespace Blazor.Tests.Tools;

public static class TestContextExtensions
{
    extension(BunitContext ctx)
    {
        public BunitContext SetupSuiteServicesWithBlazorDx(Action<ClientServiceConfigurator>? setup = null)
        {

            var env = Substitute.For<IEnvironmentInfo>();
            env.DeviceInfo.Returns(new DeviceInfo(false));

            ctx.Services.AddScoped(s => Substitute.For<IResizeObserver>());
            ctx.Services.AddScoped(s => Substitute.For<IEnvironmentInfoFactory>());
            ctx.Services.AddScoped(s => Substitute.For<ISvgImagesLoader>());
            ctx.Services.AddScoped(s => env);
            ctx.Services.AddDevExpressBlazor(options => options.BootstrapVersion = DevExpress.Blazor.BootstrapVersion.v5);
            ctx.Services.TryAddComponentRequiredServices();

            ctx.JSInterop.ConfigureJSInteropForDx();
            return ctx.SetupBlazorUiComponents(setup);
        }

        public BunitContext SetupBlazorUiComponents(Action<ClientServiceConfigurator>? setup = null)
        {
            ctx.SetupSuiteServices(setup);

            ctx.JSInterop.ConfigureJSInteropForResizeObserver();
            
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
