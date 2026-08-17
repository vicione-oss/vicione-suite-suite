using Blazor.Shared.Extensions;
using Blazor.Shared.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Sdk.Client.Services;

namespace Blazor.Shared.Components
{
    public partial class App
    {
        [Inject] private INavigationService NavigationService { get; set; } = default!;
        [Inject] private IAppRenderingProvider RenderModeProvider { get; set; } = default!;
        [Inject] private IHostEnvironment Env { get; set; } = default!;
        [Inject] private IEnumerable<IClientModuleResourceProvider> ResourceProviders { get; set; } = default!;
        [Inject] private ILogger<App> Logger { get; set; } = default!;

        public IComponentRenderMode? ConfiguredRenderMode
            => IsNavigationOnLogin() ? null : RenderModeProvider.ContentRenderMode;

        private RenderFragment RenderStylesheetResources() => builder =>
        {
            if (IsNavigationOnLogin())
                return;

            builder.AddStylesheetResources(ResourceProviders, Logger);
        };

        private RenderFragment RenderScriptResources() => builder =>
        {
            if (IsNavigationOnLogin())
                return;

            builder.AddScriptResources(ResourceProviders, Logger);
        };

        private bool IsNavigationOnLogin()
            => NavigationService.NavManager.Uri.Contains("/Account", StringComparison.OrdinalIgnoreCase);
    }
}
