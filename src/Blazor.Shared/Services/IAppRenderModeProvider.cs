using Microsoft.AspNetCore.Components;

namespace Blazor.Shared.Services;

public interface IAppRenderingProvider
{
    IComponentRenderMode HeaderRenderMode { get; }

    IComponentRenderMode ContentRenderMode { get; }

    string AppStyleSheet { get; }
}
