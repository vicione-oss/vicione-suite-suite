using System.Reflection;
using Sdk.Modules;

namespace Core.UiHosting;

public interface IUiModuleBundle
{
    IModule Module { get; }
    string AssemblyLocation { get; }
    Assembly? Assembly { get; }
}
