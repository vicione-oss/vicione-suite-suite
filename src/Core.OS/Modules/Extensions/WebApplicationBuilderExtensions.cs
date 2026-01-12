using System.IO.Abstractions;
using Core.OS.Instance;
using Core.OS.Modules.Services;
using Serilog;

namespace Core.OS.Modules.Extensions;

public static class WebApplicationBuilderExtensions
{
    /// <summary>
    /// Create and init (create suite context, validate options, load backend assemblies) <see cref="IModuleHost"/> using <see cref="ModuleHostBuilder"/>
    /// </summary>    
    public static async Task<IModuleHost> AddModuleHost(this WebApplicationBuilder builder, IFileSystem fileSystem, InstanceOptions instanceOptions, CancellationToken token = default)
    {
        AppDomain.CurrentDomain.UnhandledException -= LogUnhandledExceptionEvent;
        AppDomain.CurrentDomain.UnhandledException += LogUnhandledExceptionEvent;

        var version = fileSystem.EvaluateLocalVersionString(out var branchName);
        var branchInfo = branchName is null ? string.Empty : $" branch: '{branchName}'";

        Log.Information("Configuring '{InstanceType}' application version '{Version}'{BranchInfo}", instanceOptions.Type, version, branchInfo);

        builder.Services.AddModuleServices();

        // the module manifest is kept static because it's only loaded on startup and can't be changed on runtime
        var manifestProvider = await builder.Services.AddModuleManifestProvider(fileSystem, instanceOptions, Log.Logger, token);

        var repositoryOptions = builder.Configuration.GetArtifactRepositoryOptions();// nexus|jfrog api, user pwd
        var loaderOptions = builder.Configuration.GetModuleLoaderOptions();// module paths, flags etc.

        // combine appsettings, environment etc. with module manifest
        var additionalModules = builder.Environment.IsDevelopment() ? ModuleConstants.SampleModuleIds : [];
        var moduleOptions = builder.Configuration.CreateModuleOptions(manifestProvider, additionalModules);
        var uiHostOptions = builder.Configuration.CreateUiHostOptions(loaderOptions);
        if (uiHostOptions is not null)
            moduleOptions[loaderOptions.UiHost!] = uiHostOptions;

        WorkspaceManagement.ResetMarkedModuleWorkspace(fileSystem, instanceOptions, Log.Logger);

        var hostBuilder = new ModuleHostBuilder(fileSystem, builder.Configuration, moduleOptions)
            .WithSynchronization(manifestProvider, repositoryOptions)
            .WithSuiteDependencyContext()
            .WithOptionsSupport(builder.Configuration, builder.Services);

        var moduleHost = await hostBuilder.Build(builder.Services.AddControllersWithViews, token);

        Log.Debug("Module host created successfully");

        builder.Services.AddSingleton(moduleHost);

        return moduleHost;
    }

    private static void LogUnhandledExceptionEvent(object sender, UnhandledExceptionEventArgs e)
        => Log.Error("Unhandled error! {Error}", e.ExceptionObject);
}
