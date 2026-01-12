using System.IO.Abstractions;
using Core.Module;
using Core.Module.Extensions;
using Core.Module.JFrog;
using Core.Module.Options;
using Core.Module.Utils;
using Core.OS.Modules.Extensions;
using Microsoft.Extensions.Options;
using Sdk.Modules;

namespace Core.OS.Modules.Services;

internal sealed partial class ModuleSynchronizer
{
    private ModuleApiOptions? _apiOptions;
    private IModuleApiAdapter? _apiAdapter;
    private ModuleLoaderOptions? _loaderOptions;
    private bool _validatePackages;
    private bool _deleteOrphanedVersions;
    private string? _modulesPath;
    private List<ModuleDependencyPackage>? _packages;
    private HttpClient? _httpClient;

    private sealed class SynchronizationOptions
    {
        public required IFileSystem FileSystem { get; set; }

        public required IModuleApiAdapter ModuleApi { get; set; }

        public required List<ModuleDependencyPackage> Packages { get; set; }

        public required string ModulesPath { get; set; }

        public bool ValidatePackages { get; set; }

        public bool DeleteOrphanedPackages { get; set; }

        public required Version SdkVersion { get; set; }
    }


    public ModuleSynchronizer WithModulesPath(ModuleLoaderOptions loaderOptions)
    {
        _loaderOptions = loaderOptions;
        return this;
    }

    public ModuleSynchronizer WithModulesPath(string modulesPath)
    {
        _modulesPath = fileSystem.Path.GetFullPath(modulesPath);
        return this;
    }

    public ModuleSynchronizer WithApiAdapter(IModuleApiAdapter apiAdapter)
    {
        _apiAdapter = apiAdapter;
        return this;
    }

    public ModuleSynchronizer WithApiAdapter(ModuleApiOptions apiOptions)
    {
        _apiOptions = apiOptions;
        return this;
    }

    public ModuleSynchronizer WithPackages(IEnumerable<ModuleDependencyPackage> packages)
    {
        _packages ??= [];
        _packages.AddRange(packages);
        return this;
    }

    public ModuleSynchronizer WithPackageSdkValidation()
    {
        _validatePackages = true;
        return this;
    }

    public ModuleSynchronizer WithOrphanedVersionCleanup()
    {
        _deleteOrphanedVersions = true;
        return this;
    }

    private SynchronizationOptions BuildOptions() => new()
    {
        DeleteOrphanedPackages = _deleteOrphanedVersions,
        FileSystem = fileSystem,
        Packages = _packages ?? [],
        ModuleApi = GetModuleApiAdapter(),
        ModulesPath = GetModulesPath(),
        SdkVersion = ModuleHelpers.GetSdkAssemblyVersion(),
        ValidatePackages = _validatePackages,
    };

    private string GetModulesPath()
    {
        if (!string.IsNullOrEmpty(_modulesPath) && _loaderOptions is not null)
            throw new InvalidOperationException($"Use only one of the {nameof(WithModulesPath)} methods to configure a valid module source path.");

        if (string.IsNullOrEmpty(_modulesPath) && _loaderOptions is null)
            throw new InvalidOperationException($"Use one of the {nameof(WithModulesPath)} methods to configure a valid module API.");


        if (!string.IsNullOrEmpty(_modulesPath))
        {
            if (!fileSystem.Path.Exists(_modulesPath))
                throw new DirectoryNotFoundException(_modulesPath);

            return _modulesPath;
        }

        if (_loaderOptions is not null)
        {
            return fileSystem.GetOrCreateRootedModulesPath(_loaderOptions);
        }

        throw new InvalidOperationException($"Use one of the {nameof(WithModulesPath)} methods to configure a valid path.");
    }

    private IModuleApiAdapter GetModuleApiAdapter()
    {
        if (_apiAdapter is null && _apiOptions is null)
            throw new InvalidOperationException($"Use one of the {nameof(WithApiAdapter)} methods to configure a valid module API.");

        if (_apiAdapter is not null && _apiOptions is not null)
            throw new InvalidOperationException($"Use only one of the {nameof(WithApiAdapter)} methods to configure a valid module API.");

        if (_apiAdapter is not null)
            return _apiAdapter;

        if (_apiOptions is null)
            throw new InvalidOperationException($"Use '{nameof(WithApiAdapter)}' method to setup the module API.");

        var options = Options.Create(_apiOptions);
        _httpClient = new HttpClient();
        var queryApi = new JFrogArtifactQueryApi(fileSystem, _httpClient, options);

        return new ModuleApiAdapter(queryApi, fileSystem);
    }

    private void Dispose(bool disposing)
    {
        if (!_disposedValue)
        {
            if (disposing)
            {
                _httpClient?.Dispose();
            }

            _disposedValue = true;
        }
    }

    public void Dispose()
    {
        // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }
}
