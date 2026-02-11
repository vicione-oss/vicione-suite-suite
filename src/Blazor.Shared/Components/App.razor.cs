using Blazor.Shared.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Hosting;

namespace Blazor.Shared.Components
{
    public partial class App
    {
        [Inject] private IAppRenderingProvider RenderModeProvider { get; set; } = default!;
        [Inject] private IHostEnvironment Env { get; set; } = default!;
    }
}
