using Sdk.Backend.Modules;

namespace Core.OS.Tests;

internal static class TestModules
{
    public class ModuleTwo : BackendModule;

    public class ModuleDependsOnTestBackend : BackendModule;

    public class ModuleDependsOnTwo : BackendModule;

    public class ModuleDependsOnTestBackendAndTwo : BackendModule;
}
