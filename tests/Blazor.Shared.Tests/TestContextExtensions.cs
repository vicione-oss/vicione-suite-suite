using Blazor.Shared.Services;
using Blazor.Shared.Settings.Services;
using Blazor.Tests.Tools;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Sdk.Client.ControlPanels.Components;
using Sdk.Client.ControlPanels.Services;
using Sdk.Testing.Client;

namespace Blazor.Shared.Tests;

internal static class TestContextExtensions
{
    public static TestContext SetLocalServices(this TestContext ctx)
    {
        ctx.Services.AddSingleton(Substitute.For<INavigationService>());

        return ctx;
    }

    private static void SetupInternal(TestContext ctx)
    {
        ctx.Services.AddTransient<SettingsModuleService>();

        ctx.JSInterop.SetupModule();
        ctx.JSInterop.SetupVoid("ViciOne.AccordionMenu.init", _ => true);
    }

    private static void SetupSettingsState(TestContext ctx, Action<SettingsModuleState>? moduleState = null)
    {
        var settingsState = new SettingsModuleState();
        ctx.Services.AddSingleton(settingsState);

        moduleState?.Invoke(settingsState);
    }

    public static TestContext SetupBlazorSharedSettings(this TestContext ctx, Action<ClientServiceConfigurator>? setup = null, Action<SettingsModuleState>? moduleState = null)
    {
        SetupInternal(ctx);

        ctx.SetupSuiteServicesWithBlazorDx(setup);

        SetupSettingsState(ctx, moduleState);

        return ctx;
    }

    public static TestContext SetupControlPanelServices(this TestContext ctx,
        Action<IControlPanelRequest>? controlPanelRequestSetup = null,
        Action<IActiveControlPanelPageProvider>? activeControlPanelPageProviderSetup = null)
    {
        var controlPanelRequest = Substitute.For<IControlPanelRequest>();
        var activeControlPanelPageProvider = Substitute.For<IActiveControlPanelPageProvider>();

        ctx.Services.AddSingleton(activeControlPanelPageProvider);
        ctx.Services.AddSingleton(Substitute.For<IActiveControlPanelDescriptorProvider>());
        ctx.Services.AddSingleton(controlPanelRequest);
        ctx.Services.AddSingleton(Substitute.For<INavigateBackRequest>());

        controlPanelRequestSetup?.Invoke(controlPanelRequest);
        activeControlPanelPageProviderSetup?.Invoke(activeControlPanelPageProvider);

        return ctx;
    }

    public static IRenderedComponent<TComponent> RenderControlPanelPage<TComponent, TState>(this TestContext ctx, TState state, int index = 0)
        where TComponent : ControlPanelBase<TState>
        where TState : IControlPanelState
    {
        if (index < 0)
            throw new InvalidOperationException("Index has to be positive.");

        var pageRegistry = ctx.Services.GetRequiredService<IControlPanelPageRegistry>();
        var activePageProvider = ctx.Services.GetRequiredService<IActiveControlPanelPageProvider>();

        var component = ctx.RenderComponent<TComponent>(b => b.Add(c => c.State, state));
        var pages = pageRegistry.Select(k => k.ControlPanelPage).ToArray();
        if (index >= pages.Length)
            throw new InvalidOperationException($"No page with index {index} provided.");

        // we need to fake the active page to get it rendered
        activePageProvider
            .GetActiveControlPanelPage(Arg.Any<IControlPanelRegistryItem>())
            .Returns(pages[index]);

        // need to be rendered again to trigger add page content in
        // Sdk.Client.ControlPanels.Components.ControlPanelPage.BuildRenderTree
        component.Render();

        return component;
    }
}
