using System.IO.Abstractions;
using Core.Module;
using Core.Module.Contracts;
using Core.Module.Extensions;

namespace Suite.Deps;

internal static class SuiteDependencyContextFactory
{
    internal static SuiteDependencyContext Create(IFileSystem fileSystem, string? suitePath, IEnumerable<string> manifest)
    {
        var rootedSuitePath = fileSystem.GetRootedPath(suitePath);
        if (!Directory.Exists(rootedSuitePath))
            throw new DirectoryNotFoundException($"Can't find suite installation path {rootedSuitePath}");

        var config = ConfigurationUtils.GetSuiteConfiguration(rootedSuitePath);
        var coreDepsJsonFile = ConfigurationUtils.GetCoreDepsJsonFilePath(rootedSuitePath);
        var loaderOptions = ConfigurationUtils.GetSuiteModuleLoaderOptions(config, rootedSuitePath);
        if (string.IsNullOrEmpty(loaderOptions.UiHost))
            throw new InvalidOperationException("UiHost is not defined in the section ModuleLoader");

        var uiHostOptions = config.GetUiHostOptions(loaderOptions.UiHost);
        if (uiHostOptions is null)
            throw new InvalidOperationException($"No UiHost options found for {loaderOptions.UiHost}");

        // The modules that should get loaded.
        var moduleOptions = manifest.ToDictionary(k => k, k => new ModuleOptions());
        moduleOptions.Add(loaderOptions.UiHost, uiHostOptions);

        return new SuiteDependencyContextBuilder()
            .WithCore(coreDepsJsonFile)
            .WithUiHost(loaderOptions, moduleOptions)
            .WithBackendModules(loaderOptions, moduleOptions)
            .WithClientModules(loaderOptions, moduleOptions)
            .Build(fileSystem);
    }
}
