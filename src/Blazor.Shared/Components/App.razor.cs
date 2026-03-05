using Blazor.Shared.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Hosting;

namespace Blazor.Shared.Components
{
    public partial class App
    {
        [Inject] private INavigationService NavigationService { get; set; } = default!;
        [Inject] private IAppRenderingProvider RenderModeProvider { get; set; } = default!;
        [Inject] private IHostEnvironment Env { get; set; } = default!;

        public IComponentRenderMode? ConfiguredRenderMode
            => NavigationService.NavManager.Uri.Contains("/Account", StringComparison.OrdinalIgnoreCase) ? null : RenderModeProvider.ContentRenderMode;
    }
}
