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
    extension(TestConfig config)
    {
        public TestConfig ConfigureModuleLoader(string? modulesPath = null, List<string>? debugPaths = null)
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

        public TestConfig AddTestBackendClientModule(bool enable = true)
        {
            config.AddTestModule(TestBackendModule.Id, enable);
            return config;
        }

        public TestConfig AddTestModule(string moduleId, bool enable = true)
            => config.AddTestModuleInternal(moduleId, enable);

        private TestConfig AddTestModuleInternal(string moduleId, bool enable = true)
        {
            config.SetSetting($"{moduleId.Replace(".", "", StringComparison.Ordinal)}:{nameof(ModuleOptions.Enable)}", enable.ToString());
            return config;
        }

        public TestConfig AddTestUiHost(bool enable = true, bool useDebugPaths = true)
            => config.AddUiHost(TestUiHostBackend.Id, enable, useDebugPaths);

        private TestConfig AddUiHost(string moduleId, bool enable, bool useDebugPaths = true)
        {
            if (useDebugPaths)
                config.SetSetting($"{ModuleLoader.UiHostsPath}", GetUiHostTestDebugPath(enable));

            if (!enable)
                return config.SetSetting($"{ModuleLoader.UiHost}", string.Empty);

            return config
                .SetSetting($"{ModuleLoader.UiHost}", moduleId)
                .SetSetting(UiHost.Enable(moduleId), enable.ToString());
        }
    }

    public static string? GetUiHostTestDebugPath(bool enableUiHost)
        => enableUiHost ? Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location!)!) : null;

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
