using Blazor.Shared.Services;
using Blazor.Shared.Settings.Services;
using Blazor.Tests.Tools;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Sdk.Client.ControlPanels.Components;
using Sdk.Client.ControlPanels.Services;
using Sdk.Testing.Client;
using ViciOne.Ui.MonochromeIcons.Assets.Services;

namespace Blazor.Shared.Tests;

internal static class TestContextExtensions
{
    extension(BunitContext ctx)
    {
        public BunitContext SetLocalServices()
        {
            ctx.Services.AddSingleton(Substitute.For<INavigationService>());
            ctx.Services.AddSingleton(Substitute.For<IMonochromeIconSvgMarkupProvider>());

            return ctx;
        }

        private void SetupInternal()
        {
            ctx.Services.AddTransient<SettingsModuleService>();

            ctx.JSInterop.SetupModule();
            ctx.JSInterop.SetupVoid("ViciOne.AccordionMenu.init", _ => true);
        }

        private void SetupSettingsState(Action<SettingsModuleState>? moduleState = null)
        {
            var settingsState = new SettingsModuleState();
            ctx.Services.AddSingleton(settingsState);

            moduleState?.Invoke(settingsState);
        }

        public BunitContext SetupBlazorSharedSettings(Action<ClientServiceConfigurator>? setup = null, Action<SettingsModuleState>? moduleState = null)
        {
            ctx.SetupInternal();

            ctx.SetupBlazorUiComponents(setup);

            ctx.SetupSettingsState(moduleState);

            return ctx;
        }

        public BunitContext SetupControlPanelServices(Action<IControlPanelRequest>? controlPanelRequestSetup = null,
            Action<IActiveControlPanelPageProvider>? activeControlPanelPageProviderSetup = null)
        {
            var controlPanelRequest = Substitute.For<IControlPanelRequest>();
            ctx.Services.AddSingleton(controlPanelRequest);
            ctx.Services.AddSingleton(Substitute.For<INavigateBackRequest>());

            controlPanelRequestSetup?.Invoke(controlPanelRequest);

            return ctx;
        }

        public IRenderedComponent<TComponent> RenderControlPanelPage<TComponent, TState>(TState state, int index = 0)
            where TComponent : ControlPanelBase<TState>
            where TState : IControlPanelState
        {
            if (index < 0)
                throw new InvalidOperationException("Index has to be positive.");

            var pageRegistry = ctx.Services.GetRequiredService<IControlPanelPageRegistry>();
            var activePageProvider = ctx.Services.GetRequiredService<IActiveControlPanelPageProvider>();

            var component = ctx.Render<TComponent>(b => b.Add(c => c.State, state));
            var pages = pageRegistry.Select(k => k.ControlPanelPage).ToArray();
            if (index >= pages.Length)
                throw new InvalidOperationException($"No page with index {index} provided.");

            // The services have to be set up before the page at this index can render as the active one.
            state.ActivePageIndex = index;

            activePageProvider
                .GetActiveControlPanelPage(Arg.Any<IControlPanelRegistryItem>())
                .Returns(pages[index]);

            // Renders the active page.
            component.Render();

            return component;
        }
    }
}
