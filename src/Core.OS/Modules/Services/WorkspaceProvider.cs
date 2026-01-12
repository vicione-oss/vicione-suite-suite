using Sdk.Backend.Modules;

namespace Core.OS.Modules.Services;

internal sealed class WorkspaceProvider<TModule> : IWorkspaceProvider<TModule> where TModule : BackendModule
{
    public string Home { get; }

    public string Cache { get; }

    public WorkspaceProvider(
        IWorkspaceManagement workspaceManagement)
    {
        var moduleId = typeof(TModule) == typeof(SystemBackendModule)
            ? Sdk.Constants.SystemModuleId
            : Sdk.Modules.ModuleIdResolver.ResolveId<TModule>();

        Home = workspaceManagement.GetHomeDirectory(moduleId);
        Cache = workspaceManagement.GetCacheDirectory(moduleId);
    }
}
