using System.Reflection;
using Core.UiHosting;
using Sdk.Client.Modules;

namespace Blazor.Server.Backend.Services;

public sealed class UiModuleManager : IUiModuleManager
{
    private readonly List<IUiModuleBundle> _clientBundles = [];

    public IEnumerable<ClientModule> UiModules
        => _clientBundles.Select(k => (ClientModule)k.Module);

    public IEnumerable<Assembly> UiModuleAssemblies
        => _clientBundles.Where(k => k.Assembly is not null)
            .Select(k => k.Assembly!)
            .DistinctBy(k => k.GetName().FullName);

    public void AddModuleBundle(IUiModuleBundle moduleBundle)
        => _clientBundles.Add(moduleBundle);

    public void AddModuleBundles(IEnumerable<IUiModuleBundle> moduleBundles)
        => _clientBundles.AddRange(moduleBundles);

    public Assembly[] GetAdditionalAssemblies() => UiModuleAssemblies
        .Union([typeof(BlazorServerBackendModule).Assembly])
        .ToArray();

    public void RemoveUiModuleBundle(string moduleId)
        => _clientBundles.RemoveAll(k => k.Module.ModuleKey.ModuleId == moduleId);
}
