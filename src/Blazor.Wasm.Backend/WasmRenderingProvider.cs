using Blazor.Shared.Services;
using Blazor.Wasm.Client;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

namespace Blazor.Wasm.Backend;

internal sealed class WasmRenderingProvider : IAppRenderingProvider
{
    private IComponentRenderMode? _headerRenderMode;
    private IComponentRenderMode? _contentRenderMode;

    public IComponentRenderMode HeaderRenderMode => _headerRenderMode ??= new InteractiveWebAssemblyRenderMode(prerender: false);

    public IComponentRenderMode ContentRenderMode => _contentRenderMode ??= new InteractiveWebAssemblyRenderMode(prerender: false);

    public string AppStyleSheet => $"{typeof(BlazorWasmClientModule).Assembly.GetName().Name}.styles.css";
}
