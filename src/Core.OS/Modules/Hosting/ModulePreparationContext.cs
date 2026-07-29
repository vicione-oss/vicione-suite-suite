using System.IO.Abstractions;
using Core.Module.Contracts;
using Core.Module.Options;
using Core.OS.Hosting.Contracts;
using Core.OS.Instance;
using Core.OS.Modules.Contracts;
using Sdk.Modules;

namespace Core.OS.Modules.Hosting;

/// <summary>
/// Mutable state shared between the steps of <see cref="ModulePreparationPipeline"/>.
/// Carries the ambient inputs (builder, file system, options, logger factory) and the
/// artifacts produced along the way (manifest, repository store, options cache, module host).
/// DI registrations are deferred to the final step so an earlier abort leaves the
/// <see cref="WebApplicationBuilder.Services"/> collection untouched.
/// </summary>
internal sealed class ModulePreparationContext(
    IHostApplicationBuilder builder,
    IFileSystem fileSystem,
    InstanceOptions instanceOptions,
    IArtifactRepositoryStore repositoryStore,
    ILoggerFactory loggerFactory)
{
    private readonly ILogger logger = loggerFactory.CreateLogger(nameof(ModulePreparationContext));

    public IHostApplicationBuilder Builder { get; } = builder;

    public IFileSystem FileSystem { get; } = fileSystem;

    public InstanceOptions InstanceOptions { get; } = instanceOptions;

    public ILoggerFactory LoggerFactory { get; } = loggerFactory;

    /// <summary>Logger shared by the module preparation steps.</summary>
    public ILogger Logger => logger;

    public ModulePackageManifest? Manifest { get; set; }

    /// <summary>
    /// Shared repository store instance reused across migrate/load steps.
    /// Owned by the context and disposed when the pipeline finishes.
    /// </summary>
    public IArtifactRepositoryStore RepositoryStore => repositoryStore;

    /// <summary>Repository options cache populated during preparation, registered in the final step.</summary>
    public IArtifactRepositoryOptionsCache? RepositoryOptionsCache { get; set; }

    public ModuleLoaderOptions? ModuleLoaderOptions { get; set; }

    public IModuleOptionsStore? ModuleOptionsStore { get; set; }

    public Dictionary<string, ModuleOptions>? ModuleOptions { get; set; }

    public ModuleSynchronizationResults? SynchronizationResults { get; set; }

    /// <summary>The fully built module host, available after the final step succeeds.</summary>
    public IModuleHost? ModuleHost { get; set; }

    /// <summary>The outcome of the module preparation pipeline run.</summary>
    public IPreparationResult? Result { get; set; }
}
