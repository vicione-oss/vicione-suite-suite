using System.Reflection;
using Core.Module.Contracts;
using Core.Module.Options;
using Microsoft.Extensions.Configuration;
using Sdk.Backend.Extensions;
using Sdk.Testing.Backend;
using TestModule.Backend;
using TestModule.Client;
using TestUiHost;

namespace Core.Tests.Tools;

public static class TestConfigExtensions
{
    public static TestConfig ConfigureModuleLoader(this TestConfig config, string? modulesPath = null, List<string>? debugPaths = null)
    {
        if (string.IsNullOrEmpty(modulesPath))
        {
            var assembly = Assembly.GetExecutingAssembly();
            modulesPath = Path.GetDirectoryName(assembly.Location) ?? string.Empty;
        }

        if (debugPaths is not null)
        {
            var idx = 0;
            foreach (var item in debugPaths)
                config.SetSetting($"{ModuleLoader.ModulesDebugPath(idx++)}", item);
        }

        return config
            .SetSetting(ModuleLoader.ModulesPath, modulesPath);
    }

    public static TestConfig AddTestBackendClientModule(this TestConfig conf, bool enable = true)
    {
        conf.AddTestModule(TestBackendModule.Id, enable);
        return conf;
    }

    public static TestConfig AddTestModule(this TestConfig conf, string moduleId, bool enable = true)
        => conf.AddTestModuleInternal(moduleId, enable);

    private static TestConfig AddTestModuleInternal(this TestConfig conf, string moduleId, bool enable = true)
    {
        conf.SetSetting($"{moduleId.Replace(".", "", StringComparison.Ordinal)}:{nameof(ModuleOptions.Enable)}", enable.ToString());
        return conf;
    }

    public static TestConfig AddTestUiHost(this TestConfig conf, bool enable = true, bool useDebugPaths = true)
        => conf.AddUiHost(TestUiHostBackend.Id, enable, useDebugPaths);

    public static string? GetUiHostTestDebugPath(bool enableUiHost)
        => enableUiHost ? Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location!)!) : null;

    public static TestConfig AddUiHost(this TestConfig conf, string moduleId, bool enable, bool useDebugPaths = true)
    {
        if (useDebugPaths)
            conf.SetSetting($"{ModuleLoader.UiHostsPath}", GetUiHostTestDebugPath(enable));

        if (!enable)
            return conf.SetSetting($"{ModuleLoader.UiHost}", string.Empty);

        return conf
            .SetSetting($"{ModuleLoader.UiHost}", moduleId)
            .SetSetting(UiHost.Enable(moduleId), enable.ToString());
    }

    public static Dictionary<string, ModuleOptions> CreateModuleTestOptions(this IConfiguration configuration, ModuleLoaderOptions loaderOptions)
    {
        var results = new Dictionary<string, ModuleOptions>();

        if (!string.IsNullOrEmpty(loaderOptions.UiHost))
            results[loaderOptions.UiHost] = configuration.BindSection<UiHostOptions>(loaderOptions.UiHost);

        var testIds = new[] { TestBackendModule.Id, TestClientModule.Id };

        foreach (var testModuleId in testIds)
            results[testModuleId] = configuration.BindModuleSection<ModuleOptions>(testModuleId);

        return results;
    }

    public static string GetTypeAssemblyName(this Type type)
    {
        var assemblyName = (Assembly.GetAssembly(type)?.GetName()) ??
            throw new InvalidOperationException($"Assembly not found for type {type.FullName}");

        return assemblyName.Name ?? throw new InvalidOperationException($"Assembly name for type {type.FullName} is null");
    }

    private static class UiHost
    {
        public static string Enable(string moduleId)
            => $"{moduleId}:{nameof(UiHostOptions.Enable)}";

        public static string UseDebugRoot(string moduleId)
            => $"{moduleId}:{nameof(UiHostOptions.UseDebugRoot)}";
    }

    private static class ModuleLoader
    {
        public const string ModulesPath = $"{ModuleLoaderOptions.ConfigSection}:{nameof(ModuleLoaderOptions.ModulesPath)}";

        public const string UiHost = $"{ModuleLoaderOptions.ConfigSection}:{nameof(ModuleLoaderOptions.UiHost)}";
        public const string UiHostsPath = $"{ModuleLoaderOptions.ConfigSection}:{nameof(ModuleLoaderOptions.UiHostsPath)}";

        public static string ModulesDebugPath(int idx) => $"{ModuleLoaderOptions.ConfigSection}:{nameof(ModuleLoaderOptions.ModuleDebugPaths)}:{idx}";
    }
}
