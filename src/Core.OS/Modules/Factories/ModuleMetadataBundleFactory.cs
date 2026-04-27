using System.IO.Abstractions;
using Core.Module;
using Core.Module.Extensions;
using Core.OS.Modules.Contracts;
using Core.OS.Modules.Extensions;
using Core.Shared.Modules.Contracts;
using Sdk.Modules;

namespace Core.OS.Modules.Factories;

internal static class ModuleMetadataBundleFactory
{
    public static ModuleMetadataBundle CreateFallbackBundle(IFileSystem fileSystem, ModuleDependencyContext context)
    {
        var main = context.GetMainLibrary();
        var sdkVersion = context.GetSdkVersion();
        var metadata = new ModuleMetadata
        {
            Name = fileSystem.Path.GetFileNameWithoutExtension(context.AssemblyName),
            Description = "Generated metadata",
            Version = main.Version,
            MinSuiteSdkVersion = sdkVersion,
            Title = fileSystem.Path.GetFileNameWithoutExtension(context.AssemblyName)
        };

        return CreateBundle(context, metadata);
    }

    public static async Task<ModuleMetadataBundle> CreateDebugBundle(IFileSystem fileSystem, ModuleDependencyContext moduleContext, CancellationToken cancellationToken)
    {
        var debugMetadataPath = fileSystem.FindModuleMetadataPath(moduleContext.AssemblyFolder);
        if (string.IsNullOrEmpty(debugMetadataPath) || !fileSystem.Path.Exists(debugMetadataPath))
            return CreateFallbackBundle(fileSystem, moduleContext);

        // if we can't deserialize we'll throw
        var debugMetadata = await fileSystem.DeserializeModuleMetadata(debugMetadataPath, cancellationToken)
            ?? throw new InvalidOperationException($"Failed to load '{moduleContext.ModuleId}' debug metadata from '{debugMetadataPath}'.");

        return CreateBundle(moduleContext, debugMetadata);
    }

    public static ModuleMetadataBundle CreateBundle(ModuleDependencyContext context, ModuleMetadata metadata)
        => new()
        {
            HasBackend = context.ModuleType == ModuleType.Backend,
            HasFrontend = context.ModuleType == ModuleType.Client,
            Metadata = metadata,
            Installed = true,
            ModuleId = metadata.Name,
            Errors = context.GetErrorInfos(),
            IsDebugSource = context.IsDebugSource,
        };

    public static ModuleMetadataBundle CreateErrorBundle(ModuleSynchronizationResult result, string minSdkVersion)
        => new()
        {
            ModuleId = result.Name,
            Installed = true,
            Metadata = new ModuleMetadata
            {
                Name = result.Name,
                Title = result.Name,
                Version = result.Version?.ToString() ?? ModuleConstants.UnresolvedVersionMarker,
                Description = "Module can't be loaded - check logs for errors",
                MinSuiteSdkVersion = minSdkVersion,
            },
            Errors = result.Error is not null ? [result.Error] : []
        };
}
