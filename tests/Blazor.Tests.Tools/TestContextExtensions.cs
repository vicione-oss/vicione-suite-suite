using Bunit;
using DevExpress.Blazor.Internal;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Sdk.Testing.Client;
using ViciOne.Ui.Blazor.Components.CheckBox.Extensions;
using ViciOne.Ui.Blazor.Components.SpinEdit.Extensions;
using ViciOne.Ui.Shared.Dx.Components.Resizing;

namespace Blazor.Tests.Tools;

public static class TestContextExtensions
{
    public static TestContext SetupSuiteServicesWithBlazorDx(this TestContext ctx, Action<ClientServiceConfigurator>? setup = null)
    {
        ctx.SetupSuiteServices(setup);

        var env = Substitute.For<IEnvironmentInfo>();
        env.DeviceInfo.Returns(new DeviceInfo(false));

        ctx.Services.AddScoped(s => Substitute.For<IResizeObserver>());
        ctx.Services.AddScoped(s => Substitute.For<IEnvironmentInfoFactory>());
        ctx.Services.AddScoped(s => Substitute.For<ISvgImagesLoader>());
        ctx.Services.AddScoped(s => env);
        ctx.Services.AddDevExpressBlazor(options => options.BootstrapVersion = DevExpress.Blazor.BootstrapVersion.v5);
        ctx.Services.TryAddComponentRequiredServices();

        ctx.JSInterop.ConfigureJSInterop();
        return ctx;
    }

    public static TestContext SetupBlazorUiComponents(this TestContext ctx, Action<ClientServiceConfigurator>? setup = null)
    {
        ctx.Services.AddCheckBox()
            .AddShortSpinEdit()
            .AddIntSpinEdit()
            .AddFloatSpinEdit();

        return ctx;
    }

    /// <summary>
    /// Possible workaround for bUnit integration with DevExpress Blazor controls provided at
    /// https://supportcenter.devexpress.com/ticket/details/t1056787/devexpress-and-bunit-support
    /// </summary>
    public static BunitJSInterop ConfigureJSInterop(this BunitJSInterop interop)
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
    public static BunitJSInterop ConfigureQuickGridJSInterop(this BunitJSInterop interop)
    {
        interop.Mode = JSRuntimeMode.Loose;

        var rootModule = interop.SetupModule("./_content/Microsoft.AspNetCore.Components.QuickGrid/QuickGrid.razor.js");
        rootModule.Mode = JSRuntimeMode.Loose;

        return interop;
    }
}
