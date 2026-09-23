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
using Mono.TextTemplating.CodeCompilation;
using Sdk.Messaging;
using Sdk.Modules;

namespace Core.OS.Modules.Extensions;

internal static class IFileSystemExtensions
{
    private const string PipelineVersionKey = "##VERSION_REF##";

    extension(IFileSystem fileSystem)
    {
        public async Task EnsureModuleVersionsFile(InstanceOptions instanceOptions, string? manifestSeedingPath, ILogger logger, CancellationToken cancellationToken = default)
        {
            var modulesFilePath = GetModuleVersionsFilePath(fileSystem, instanceOptions);
            if (fileSystem.File.Exists(modulesFilePath))
            {
                logger.LogInformation("Use existing module version file from '{FilePath}'", modulesFilePath);
                return;
            }

            try
            {
                if (!string.IsNullOrWhiteSpace(manifestSeedingPath))
                {
                    await CopyModuleManifestSeedTo(fileSystem, manifestSeedingPath, modulesFilePath, cancellationToken);

                    logger.LogInformation("Use module version seeding file '{FilePath}'", manifestSeedingPath);
                    return;
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed on module versions from seeding file '{FilePath}'", modulesFilePath);
            }

            try
            {
                await CopyInitialModuleManifestTo(fileSystem, modulesFilePath, cancellationToken);

                logger.LogInformation("Use empty initial module versions file");
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to create initial module versions file '{FilePath}'", modulesFilePath);
            }
        }

        private async Task CopyModuleManifestSeedTo(string manifestSeedingPath, string modulesFilePath, CancellationToken cancellationToken)
        {
            await using var fs = fileSystem.FileStream.New(manifestSeedingPath, new FileStreamOptions()
            {
                Mode = FileMode.Open,
                Access = FileAccess.Read,
                Share = FileShare.Read,
                Options = FileOptions.Asynchronous,
            });

            // Deserialized only to validate the seeded manifest; the result is discarded.
            _ = await JsonSerializer.DeserializeAsync<ModulePackageManifest>(fs, DefaultJsonSerializerSettings.Default, cancellationToken)
                ?? throw new InvalidOperationException($"Failed to deserialize modules versions file '{modulesFilePath}'.");

            fileSystem.File.Copy(manifestSeedingPath, modulesFilePath);
        }

        public async Task CopyInitialModuleManifestTo(string modulesFilePath, CancellationToken cancellationToken)
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

        public string GetModuleVersionsFilePath(InstanceOptions instanceOptions)
            => fileSystem.Path.Combine(fileSystem.GetRootedHomeDirectory(instanceOptions), ModuleConstants.ModulesFileName);

        public string GetModuleVersionsBackupFilePath(InstanceOptions instanceOptions)
        {
            var fileName = $"{fileSystem.Path.GetFileNameWithoutExtension(ModuleConstants.ModulesFileName)}-recovery-{DateTimeOffset.UtcNow.Ticks}.json";

            return fileSystem.Path.Combine(fileSystem.GetRootedHomeDirectory(instanceOptions), fileName);
        }

        public string GetModulePackageOperationsFilePath(InstanceOptions instanceOptions)
            => fileSystem.Path.Combine(fileSystem.GetRootedHomeDirectory(instanceOptions), ModuleConstants.ModuleOperationsFileName);

        public string GetModulePackageOperationsSentinelFilePath(InstanceOptions instanceOptions)
            => fileSystem.Path.Combine(fileSystem.GetRootedHomeDirectory(instanceOptions), ModuleConstants.ModuleOperationsSentinelFileName);

        public string GetModuleReconcileSignatureFilePath(InstanceOptions instanceOptions)
            => fileSystem.Path.Combine(fileSystem.GetRootedHomeDirectory(instanceOptions), ModuleConstants.ModuleReconcileSignatureFileName);

        public string GetOrCreateRootedModulesPath(ModuleLoaderOptions loaderOptions)
        {
            var rootedModulesPath = fileSystem.GetRootedModulesPath(loaderOptions);
            if (!fileSystem.Directory.Exists(rootedModulesPath))
                fileSystem.Directory.CreateDirectory(rootedModulesPath);

            return rootedModulesPath;
        }

        private string GetRootedModulesPath(ModuleLoaderOptions loaderOptions)
            => fileSystem.GetRootedPath(loaderOptions.ModulesPath);

        public string GetModuleMetadataPath(string modulesPath, ModuleDependencyPackage package)
            => fileSystem.Path.Combine(modulesPath, package.Name, package.Version, ModuleHelpers.GetLocalMetadataFileName(package.Name));

        public async Task<ModuleMetadata> DeserializeModuleMetadata(ModuleDependencyContext moduleContext, CancellationToken cancellationToken = default)
        {
            var metadataName = ModuleHelpers.GetLocalMetadataFileName(fileSystem.Path.GetFileNameWithoutExtension(moduleContext.AssemblyName));

            var metadataJsonPath = fileSystem.Path.Combine(moduleContext.AssemblyFolder, metadataName);

            if (!fileSystem.Path.Exists(metadataJsonPath))
                throw new FileNotFoundException($"Metadata json file of module '{moduleContext.ModuleId}' is missing.");

            return await fileSystem.DeserializeModuleMetadata(metadataJsonPath, cancellationToken)
                   ?? throw new InvalidOperationException($"Failed to deserialize metadata for module '{moduleContext.ModuleId}'.");
        }

        public async Task<ModuleMetadata?> DeserializeModuleMetadata(string metadataJsonPath, CancellationToken cancellationToken = default)
        {
            await using var fs = fileSystem.FileStream.New(metadataJsonPath, FileMode.Open, FileAccess.Read);

            return await JsonSerializer.DeserializeAsync<ModuleMetadata>(fs, ModuleSerializerOptions.GetOptions(), cancellationToken);
        }

        public string CreateModuleAppDataDirectory(InstanceOptions instanceOptions, string moduleId)
        {
            var moduleAppData = fileSystem.Path.Combine(fileSystem.GetRootedHomeDirectory(instanceOptions), moduleId);
            fileSystem.Directory.CreateDirectory(moduleAppData);

            return moduleAppData;
        }

        /// <summary>
        /// Writes a file atomically: the content goes to a temporary sibling and is then moved into
        /// place, so a crash mid-write cannot leave a truncated file.
        /// </summary>
        /// <remarks>
        /// The move replaces the target's directory entry, so the temp file's metadata becomes the
        /// target's. An existing target's unix permissions are captured and re-applied, keeping a custom
        /// mode such as <c>0600</c>; a new file keeps the defaults. Ownership and windows ACLs are not
        /// preserved. On failure the temp file is removed best-effort and the original is left untouched.
        /// </remarks>
        /// <param name="filePath">Target file path. Missing parent directories are created.</param>
        /// <param name="writeContent">Callback receiving the writable stream of the temporary file.</param>
        public async Task WriteFileAtomic(string filePath, Func<Stream, Task> writeContent, CancellationToken cancellationToken = default)
        {
            var directory = fileSystem.Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(directory) && !fileSystem.Directory.Exists(directory))
                fileSystem.Directory.CreateDirectory(directory);

            // Write to a temp file first and move it so a crash can't leave a corrupt file.
            var tempPath = filePath + ".tmp";

            if (fileSystem.File.Exists(tempPath))
                fileSystem.File.Delete(tempPath);

            // The move replaces the target's directory entry, so the temp file's permissions become the
            // permissions of the target. Capture the existing ones to restore them after writing
            var tempFileOptions = fileSystem.CreateTempFileOptions(filePath, out var existingFileMode);

            try
            {
                await using (var stream = fileSystem.FileStream.New(tempPath, tempFileOptions))
                {
                    await writeContent(stream);
                    await stream.FlushAsync(cancellationToken);
                }

                // UnixCreateMode is filtered by the process umask and only applies to a file that did not
                // exist yet, so the mode has to be applied explicitly - chmod is not umask filtered.
                if (existingFileMode is not null && !OperatingSystem.IsWindows())
                    fileSystem.File.SetUnixFileMode(tempPath, existingFileMode.Value);

                fileSystem.File.Move(tempPath, filePath, overwrite: true);
            }
            catch
            {
                // A partially written temp file must not be left behind.
                fileSystem.TryDeleteFile(tempPath);
                throw;
            }
        }

        public void TryDeleteFile(string filePath)
        {
            if (fileSystem.File.Exists(filePath))
            {
                try
                {
                    fileSystem.File.Delete(filePath);
                }
                catch
                {
                    // Ignored: the cleanup is best effort.
                }
            }
        }

        private FileStreamOptions CreateTempFileOptions(string filePath, out UnixFileMode? existingFileMode)
        {
            var tempFileOptions = new FileStreamOptions
            {
                Mode = FileMode.Create,
                Access = FileAccess.Write,
                Share = FileShare.None,
                Options = FileOptions.Asynchronous,
            };

            if (!OperatingSystem.IsWindows() && fileSystem.File.Exists(filePath))
            {
                existingFileMode = fileSystem.File.GetUnixFileMode(filePath);
                tempFileOptions.UnixCreateMode = existingFileMode.Value;
            }
            else
            {
                existingFileMode = null;
            }

            return tempFileOptions;
        }

        /// <summary>
        /// Finds <see cref="ModuleConstants.MetadataFileName"/> in the module debug folder or an ancestor.
        /// </summary>
        public string? FindModuleMetadataPath(string moduleSrcPath)
        {
            var parentFolder = fileSystem.GetPathContains(moduleSrcPath, [ModuleConstants.MetadataFileName]);
            if (string.IsNullOrEmpty(parentFolder))
                return null;

            return fileSystem.Path.Combine(parentFolder, ModuleConstants.MetadataFileName);
        }

        /// <summary>
        /// Starts to search first file matching an item from fileNames and matching the fileFilter within startPath.
        /// If startPath does not contain a matching file its parents get searched upwards.
        /// </summary>
        private string? GetPathContains(string startPath, IList<string> fileNames, string fileFilter = "*.json")
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
        internal string EvaluateLocalVersionString(out string? branchName)
        {
            var assembly = Assembly.GetAssembly(typeof(LocalInstanceInformationProvider));
            var appPath = fileSystem.Path.GetDirectoryName(assembly!.Location);
            branchName = null;
            if (!fileSystem.Directory.Exists(appPath))
                return ModuleHelpers.GetNormalizedVersion(assembly);

            // The file is deployed by apt, so the path exists.
            var versionPath = fileSystem.Path.Combine(appPath, "version.json");
            if (!fileSystem.File.Exists(versionPath))
                return ModuleHelpers.GetNormalizedVersion(assembly);

            try
            {
                // containing e.g. { "Version": "v0.38.1 (40454a3e)" }
                var resDict = JsonSerializer.Deserialize<Dictionary<string, string>?>(fileSystem.File.ReadAllText(versionPath));
                if (resDict is null || resDict.Count == 0 || !resDict.TryGetValue("Version", out var versionString))
                    return ModuleHelpers.GetNormalizedVersion(assembly);

                // A name such as 4c3b3a3d-1276-directory-build-props carries no usable version.
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

        public IEnumerable<ModulePackageVersionPath> GetModulePackageVersionPaths(string rootedModulesPath)
        {
            // All module directories and their versions:
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

                // e.g. 0.21.1-ci2132312
                if (!SemVersion.TryParse(version, out _))
                    continue;

                result.Add(new ModulePackageVersionPath(name, version, dir));
            }

            return result;
        }
    }
}
