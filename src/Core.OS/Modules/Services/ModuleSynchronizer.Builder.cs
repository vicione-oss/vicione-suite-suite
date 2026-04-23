using System.IO.Abstractions;
using Core.Artifacts;
using Core.Module;
using Core.Module.Options;
using Core.Module.Utils;
using Core.OS.Modules.Extensions;
using Sdk.Modules;
using Semver;

namespace Core.OS.Modules.Services;

internal sealed partial class ModuleSynchronizer : IDisposable
{
    private IArtifactRepositoryOptionsProvider? _apiOptionsProvider;
    private IModuleArtifactRepository? _apiAdapter;
    private ModuleLoaderOptions? _loaderOptions;
    private bool _validatePackages;
    private bool _deleteOrphanedVersions;
    private string? _modulesPath;
    private List<ModuleDependencyPackage>? _packages;
    private LowHttpFactory? _httpClientFactory;
    private LoggerFactory? _loggerFactory;

    private sealed class SynchronizationOptions
    {
        public required IFileSystem FileSystem { get; set; }

        public required IModuleArtifactRepository ModuleRepository { get; set; }

        public required List<ModuleDependencyPackage> Packages { get; set; }

        public required string ModulesPath { get; set; }

        public bool ValidatePackages { get; set; }

        public bool DeleteOrphanedPackages { get; set; }

        public required SemVersion SdkVersion { get; set; }
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

    public ModuleSynchronizer WithApiAdapter(IModuleArtifactRepository apiAdapter)
    {
        _apiAdapter = apiAdapter;
        return this;
    }

    public ModuleSynchronizer WithApiAdapter(IArtifactRepositoryOptionsProvider apiOptionsProvider)
    {
        _apiOptionsProvider = apiOptionsProvider;
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
        ModuleRepository = GetModuleRepository(),
        ModulesPath = GetModulesPath(),
        SdkVersion = SemVersion.FromVersion(ModuleHelpers.GetSdkAssemblyVersion()),
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

    private IModuleArtifactRepository GetModuleRepository()
    {
        if (_apiAdapter is null && _apiOptionsProvider is null)
            throw new InvalidOperationException($"Use one of the {nameof(WithApiAdapter)} methods to configure a valid module API.");

        if (_apiAdapter is not null && _apiOptionsProvider is not null)
            throw new InvalidOperationException($"Use only one of the {nameof(WithApiAdapter)} methods to configure a valid module API.");

        if (_apiAdapter is not null)
            return _apiAdapter;

        if (_apiOptionsProvider is null)
            throw new InvalidOperationException($"Use '{nameof(WithApiAdapter)}' method to setup the module API.");

        _loggerFactory = new LoggerFactory();
        _httpClientFactory = new LowHttpFactory();

        var artifactory = ArtifactRepositoryFactory.Create(fileSystem, _httpClientFactory, _apiOptionsProvider, _loggerFactory);

        return new ModuleArtifactRepository(artifactory, fileSystem);
    }

    public void Dispose()
    {
        _httpClientFactory?.Dispose();
        _loggerFactory?.Dispose();
    }

    /// <summary>
    /// We can't have a service provider yet and therefore need to provide one
    /// that takes care of clients disposal itself
    /// </summary>
    private class LowHttpFactory : IHttpClientFactory, IDisposable
    {
        private readonly Dictionary<string, HttpClient> _httpClients = [];

        public HttpClient CreateClient(string name)
        {
            if (_httpClients.TryGetValue(name, out var httpClient))
                return httpClient;

            _httpClients[name] = new();
            return _httpClients[name];
        }
        public void Dispose()
        {
            foreach (var client in _httpClients.Values)
                client.Dispose();
        }
    }
}
