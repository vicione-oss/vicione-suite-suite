using System.IO.Abstractions;
using System.Reflection;
using System.Text.Json;
using Core.Module;
using Core.Module.Extensions;
using Core.Module.Options;
using Core.Module.Utils;
using Core.OS.Instance;
using Core.OS.Instance.Extensions;
using Core.OS.Instance.Services;
using Core.OS.Modules.Contracts;
using Core.OS.Modules.Services;
using Mono.TextTemplating.CodeCompilation;
using Sdk.Messaging;
using Sdk.Modules;

namespace Core.OS.Modules.Extensions;

internal static class IFileSystemExtensions
{
    private const string PipelineVersionKey = "##VERSION_REF##";

    public static async Task EnsureModuleVersionsFile(this IFileSystem fileSystem, InstanceOptions instanceOptions, string? manifestSeedingPath, Serilog.ILogger logger, CancellationToken cancellationToken = default)
    {
        var modulesFilePath = GetModuleVersionsFilePath(fileSystem, instanceOptions);
        if (fileSystem.File.Exists(modulesFilePath))
        {
            logger.Information("Use existing module version file from '{FilePath}'", modulesFilePath);
            return;
        }

        try
        {
            if (!string.IsNullOrWhiteSpace(manifestSeedingPath))
            {
                await CopyModuleManifestSeedTo(fileSystem, manifestSeedingPath, modulesFilePath, cancellationToken);

                logger.Information("Use module version seeding file '{FilePath}'", manifestSeedingPath);
                return;
            }
        }
        catch (Exception ex)
        {
            logger.Error(ex, "Failed on module versions from seeding file '{FilePath}'", modulesFilePath);
        }

        try
        {
            await CopyInitialModuleManifestTo(fileSystem, modulesFilePath, cancellationToken);

            logger.Information("Use empty initial module versions file");
        }
        catch (Exception ex)
        {
            logger.Error(ex, "Failed to create initial module versions file '{FilePath}'", modulesFilePath);
        }
    }

    private static async Task CopyModuleManifestSeedTo(this IFileSystem fileSystem, string manifestSeedingPath, string modulesFilePath, CancellationToken cancellationToken)
    {
        await using var fs = fileSystem.FileStream.New(manifestSeedingPath, new FileStreamOptions()
        {
            Mode = FileMode.Open,
            Access = FileAccess.Read,
            Share = FileShare.Read,
            Options = FileOptions.Asynchronous,
        });

        // validate the seeded manifest
        _ = await JsonSerializer.DeserializeAsync<ModulePackageManifest>(fs, DefaultJsonSerializerSettings.Default, cancellationToken)
            ?? throw new InvalidOperationException($"Failed to deserialize modules versions file '{modulesFilePath}'.");

        fileSystem.File.Copy(manifestSeedingPath, modulesFilePath);
    }

    public static async Task CopyInitialModuleManifestTo(this IFileSystem fileSystem, string modulesFilePath, CancellationToken cancellationToken)
    {
        var assembly = Assembly.GetAssembly(typeof(Program));

        await using var stream = assembly!.GetManifestResourceStream(ModuleConstants.InitialModulesJsonResourceKey);
        await using var fs = fileSystem.FileStream.New(modulesFilePath, new FileStreamOptions()
        {
            Mode = FileMode.Create,
            Access = FileAccess.Write,
            Share = FileShare.None,
            Options = FileOptions.Asynchronous,
        });

        await stream!.CopyToAsync(fs, cancellationToken);
    }

    public static string GetModuleVersionsFilePath(this IFileSystem fileSystem, InstanceOptions instanceOptions)
        => fileSystem.Path.Combine(fileSystem.GetRootedHomeDirectory(instanceOptions), ModuleConstants.ModulesFileName);

    public static string GetModuleVersionsBackupFilePath(this IFileSystem fileSystem, InstanceOptions instanceOptions)
    {
        var fileName = $"{fileSystem.Path.GetFileNameWithoutExtension(ModuleConstants.ModulesFileName)}-recovery-{DateTimeOffset.UtcNow.Ticks}.json";

        return fileSystem.Path.Combine(fileSystem.GetRootedHomeDirectory(instanceOptions), fileName);
    }

    public static string GetOrCreateRootedModulesPath(this IFileSystem fileSystem, ModuleLoaderOptions loaderOptions)
    {
        // ensure the download path exists
        var rootedModulesPath = fileSystem.GetRootedModulesPath(loaderOptions);
        if (!fileSystem.Directory.Exists(rootedModulesPath))
            fileSystem.Directory.CreateDirectory(rootedModulesPath);

        return rootedModulesPath;
    }

    private static string GetRootedModulesPath(this IFileSystem fileSystem, ModuleLoaderOptions loaderOptions)
        => fileSystem.GetRootedPath(loaderOptions.ModulesPath);

    public static string GetModuleMetadataPath(this IFileSystem fileSystem, string modulesPath, ModuleDependencyPackage package)
        => fileSystem.Path.Combine(modulesPath, package.Name, package.Version, ModuleHelpers.GetLocalMetadataFileName(package.Name));

    public static async Task<ModuleMetadata> DeserializeModuleMetadata(this IFileSystem fileSystem, ModuleDependencyContext moduleContext, CancellationToken cancellationToken = default)
    {
        var metadataName = ModuleHelpers.GetLocalMetadataFileName(fileSystem.Path.GetFileNameWithoutExtension(moduleContext.AssemblyName));

        var metadataJsonPath = fileSystem.Path.Combine(moduleContext.AssemblyFolder, metadataName);

        if (!fileSystem.Path.Exists(metadataJsonPath))
            throw new FileNotFoundException($"Metadata json file of module '{moduleContext.ModuleId}' is missing.");

        return await fileSystem.DeserializeModuleMetadata(metadataJsonPath, cancellationToken)
            ?? throw new InvalidOperationException($"Failed to deserialize metadata for module '{moduleContext.ModuleId}'.");
    }

    public static async Task<ModuleMetadata?> DeserializeModuleMetadata(this IFileSystem fileSystem, string metadataJsonPath, CancellationToken cancellationToken = default)
    {
        await using var fs = fileSystem.FileStream.New(metadataJsonPath, FileMode.Open, FileAccess.Read);

        return await JsonSerializer.DeserializeAsync<ModuleMetadata>(fs, ModuleSerializerOptions.GetOptions(), cancellationToken);
    }

    public static string CreateModuleAppDataDirectory(this IFileSystem fileSystem, InstanceOptions instanceOptions, string moduleId)
    {
        var moduleAppData = fileSystem.Path.Combine(fileSystem.GetRootedHomeDirectory(instanceOptions), moduleId);
        fileSystem.Directory.CreateDirectory(moduleAppData);

        return moduleAppData;
    }

    /// <summary>
    /// Try to get full path to <see cref="ModuleMetadataCache.MetadataFileName"/> in module debug folder or one of its ancestors
    /// </summary>    
    public static string? FindModuleMetadataPath(this IFileSystem fileSystem, string moduleSrcPath)
    {
        var parentFolder = fileSystem.GetPathContains(moduleSrcPath, [ModuleMetadataCache.MetadataFileName]);
        if (string.IsNullOrEmpty(parentFolder))
            return null;

        return fileSystem.Path.Combine(parentFolder, ModuleMetadataCache.MetadataFileName);
    }

    /// <summary>
    /// Starts to search first file matching an item from fileNames and matching the fileFilter within startPath.
    /// If startPath does not contain a matching file its parents get searched upwards.
    /// </summary>
    /// <param name="fileSystem"></param>
    /// <param name="startPath"></param>
    /// <param name="fileNames"></param>
    /// <param name="fileFilter"></param>
    /// <returns></returns>
    private static string? GetPathContains(this IFileSystem fileSystem, string startPath, IList<string> fileNames, string fileFilter = "*.json")
    {
        for (var directory = fileSystem.DirectoryInfo.New(startPath); directory.Parent is not null; directory = directory.Parent)
        {
            if (directory.EnumerateFiles(fileFilter)
                .Any(f => fileNames.Any(x => f.Name.Equals(x, StringComparison.OrdinalIgnoreCase))))
            {
                return directory.FullName;
            }
        }

        return null;
    }

    /// <summary>
    /// Try to get the version from 'version.json' located in application folder. It contains a ##VERSION_REF## key
    /// updated by the pipeline to e.g. 'v0.38.1 (40454a3e)' or [pipeline-id][branchname] like
    /// '4c3b3a3d-1276-directory-build-props-remove-deletecssimporttask'
    /// </summary>
    internal static string EvaluateLocalVersionString(this IFileSystem fileSystem, out string? branchName)
    {
        var assembly = Assembly.GetAssembly(typeof(LocalInstanceInformationProvider));
        var appPath = fileSystem.Path.GetDirectoryName(assembly!.Location);
        branchName = null;
        if (!fileSystem.Directory.Exists(appPath))
            return ModuleHelpers.GetNormalizedVersion(assembly);

        // path should exist because this file gets deployed by apt
        var versionPath = fileSystem.Path.Combine(appPath, "version.json");
        if (!fileSystem.File.Exists(versionPath))
            return ModuleHelpers.GetNormalizedVersion(assembly);

        try
        {
            // containing e.g. { "Version": "v0.38.1 (40454a3e)" }
            var resDict = JsonSerializer.Deserialize<Dictionary<string, string>?>(fileSystem.File.ReadAllText(versionPath));
            if (resDict is null || resDict.Count == 0 || !resDict.TryGetValue("Version", out var versionString))
                return ModuleHelpers.GetNormalizedVersion(assembly);

            // we can't get any useful version from e.g. 4c3b3a3d-1276-directory-build-props
            if (!PipelineVersionParser.TryParse(versionString, out var parsed, out _))
            {
                branchName = versionString == PipelineVersionKey ? "local development" : versionString;
                return ModuleHelpers.GetNormalizedVersion(assembly);
            }

            // -> 0.38.1
            return parsed;
        }
        catch (JsonException)
        {
            return ModuleHelpers.GetNormalizedVersion(assembly);
        }
    }

    public static IEnumerable<ModulePackageVersionPath> GetModulePackageVersionPaths(this IFileSystem fileSystem, string rootedModulesPath)
    {
        // get all module directories and versions
        // module-path/{Module}/{Version} e.g. suite-modules/ViciOne.Suite.DataCollectionWizard/0.10.0
        var moduleVersionDirectories = fileSystem.Directory
            .GetDirectories(rootedModulesPath, "*.*", SearchOption.AllDirectories)
            .Select(p => fileSystem.Path.GetRelativePath(rootedModulesPath, p))
            .Where(f => f.Count(k => k == fileSystem.Path.DirectorySeparatorChar) == 1);

        var result = new List<ModulePackageVersionPath>();

        foreach (var dir in moduleVersionDirectories)
        {
            // {Module}/{Version}
            var split = dir.Split(fileSystem.Path.DirectorySeparatorChar);
            if (split.Length != 2)
                continue;

            var name = split[0];
            var version = split[1];

            // eg. 0.21.1-ci2132312
            if (!SemVersion.TryParse(version, out _))
                continue;

            result.Add(new ModulePackageVersionPath(name, version, dir));
        }

        return result;
    }
}
