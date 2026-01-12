using Blazor.Shared.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

namespace Blazor.Server.Backend.Services;

internal sealed class ServerRenderingProvider : IAppRenderingProvider
{
    private IComponentRenderMode? _headerRenderMode;
    private IComponentRenderMode? _contentRenderMode;

    public IComponentRenderMode HeaderRenderMode => _headerRenderMode ??= new InteractiveServerRenderMode(prerender: false);

    public IComponentRenderMode ContentRenderMode => _contentRenderMode ??= new InteractiveServerRenderMode(prerender: false);

    public string AppStyleSheet => $"{typeof(ServerRenderingProvider).Assembly.GetName().Name}.styles.css";
}
