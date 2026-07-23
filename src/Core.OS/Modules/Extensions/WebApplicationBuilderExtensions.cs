using System.IO.Abstractions;
using Core.OS.Instance;
using Core.OS.Instance.Services;
using Core.OS.Modules.Services;
using Microsoft.Extensions.Options;

namespace Core.OS.Modules.Extensions;

public static class WebApplicationBuilderExtensions
{
    /// <summary>
    /// Create and init (create suite context, validate options, load backend assemblies) <see cref="IModuleHost"/> using <see cref="ModuleHostBuilder"/>
    /// </summary>    
    public static async Task<IModuleHost> AddModuleHost(this WebApplicationBuilder builder, IFileSystem fileSystem, InstanceOptions instanceOptions, ILoggerFactory loggerFactory, CancellationToken token = default)
    {
        AppDomain.CurrentDomain.UnhandledException -= LogUnhandledExceptionEvent;
        AppDomain.CurrentDomain.UnhandledException += LogUnhandledExceptionEvent;

        var version = fileSystem.EvaluateLocalVersionString(out var branchName);
        var branchInfo = branchName is null ? string.Empty : $" branch: '{branchName}'";

        var logger = loggerFactory.CreateLogger(nameof(AddModuleHost));

        logger.LogInformation("Configuring '{InstanceType}' application version '{Version}'{BranchInfo}", instanceOptions.Type, version, branchInfo);

        builder.Services.AddModuleServices();

        // now apply enqueued package operations to the manifest and store it if there were any changes.
        var manifest = await ModulePackageOperationProcessor.ApplyEnqueuedOperations(fileSystem, instanceOptions, loggerFactory, token);

        // migrate repositories from configuration to file if needed using a temporary instance of the store.
        // we will create the real one with options support in AddModuleArtifactQueryApi extension
        using var repositoryStore = new ArtifactRepositoryStore(fileSystem, Options.Create(instanceOptions));
        await repositoryStore.MigrateConfiguredRepositories(builder.Configuration, token);

        // TODO: we need to refresh the options cache on changes to repositories
        var repositoryOptionsProvider = new ArtifactRepositoryOptionsCache(builder.Configuration);
        await repositoryOptionsProvider.ReloadOptions(repositoryStore, token);
        builder.Services.AddSingleton<IArtifactRepositoryOptionsCache>(repositoryOptionsProvider);

        var loaderOptions = builder.Configuration.GetModuleLoaderOptions();// module paths, flags etc.

        // combine appsettings, environment etc. with module manifest
        var additionalModules = builder.Environment.IsDevelopment() ? ModuleConstants.SampleModuleIds : [];
        var moduleOptions = builder.Configuration.CreateModuleOptions(manifest, additionalModules);
        var uiHostOptions = builder.Configuration.CreateUiHostOptions(loaderOptions);
        if (uiHostOptions is not null)
            moduleOptions[loaderOptions.UiHost!] = uiHostOptions;

        var hostBuilder = new ModuleHostBuilder(fileSystem, builder.Configuration, moduleOptions)
            .WithSynchronization(manifest, repositoryOptionsProvider)
            .WithSuiteDependencyContext()
            .WithOptionsSupport(builder.Configuration, builder.Services)
            .WithLoggerFactory(loggerFactory);

        var moduleHost = await hostBuilder.Build(builder.Services.AddControllersWithViews, token);

        logger.LogDebug("Module host created successfully");

        builder.Services.AddSingleton(moduleHost);

        return moduleHost;
    }

    private static void LogUnhandledExceptionEvent(object sender, UnhandledExceptionEventArgs e)
        => Serilog.Log.Error("Unhandled error! {Error}", e.ExceptionObject);
}
