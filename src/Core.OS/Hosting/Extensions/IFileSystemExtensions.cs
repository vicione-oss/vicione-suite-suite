using System.IO.Abstractions;
using Core.OS.Hosting.Contracts;
using Core.OS.Instance;
using Core.OS.Instance.Extensions;
using Semver;

namespace Core.OS.Hosting.Extensions;

internal static class IFileSystemExtensions
{
    extension(IFileSystem fileSystem)
    {
        internal string GetLocalDataVersionFilePath(InstanceOptions instanceOptions)
            => fileSystem.Path.Combine(fileSystem.GetRootedHomeDirectory(instanceOptions), Constants.DataVersionFileName);

        public bool DataVersionFileExists(InstanceOptions options)
            => fileSystem.File.Exists(fileSystem.GetLocalDataVersionFilePath(options));

        public Task WriteDataVersionFile(InstanceOptions options, string version, CancellationToken cancellationToken = default)
        {
            var dataVersionPath = fileSystem.GetLocalDataVersionFilePath(options);

            // good for short strings
            return fileSystem.File.WriteAllTextAsync(dataVersionPath, version, cancellationToken);
        }

        public Task<string> ReadDataVersionFile(InstanceOptions options, CancellationToken cancellationToken = default)
        {
            var dataVersionPath = fileSystem.GetLocalDataVersionFilePath(options);

            // good for short strings
            return fileSystem.File.ReadAllTextAsync(dataVersionPath, cancellationToken);
        }

        /// <summary>
        /// Compares the running suite version against the data version persisted on disk.
        /// </summary>
        /// </list>
        /// An <see cref="InvalidOperationException"/> is thrown if the persisted version file exists but is empty,
        /// after restoring the file to the current version.
        /// </remarks>
        /// <param name="options">Instance options used to locate the data-version file.</param>
        /// <param name="logger">Logger for informational and warning messages about version transitions.</param>
        /// <param name="cancellationToken">Token to cancel asynchronous file operations.</param>
        /// <returns>
        /// <see langword="null"/> when no downgrade action is required;
        /// otherwise a <see cref="VersionDowngradeInformation"/> describing the current and persisted versions.
        /// </returns>
        public async Task<VersionDowngradeInformation?> DetectVersionDowngrade(InstanceOptions options, Serilog.ILogger logger, CancellationToken cancellationToken = default)
        {
            var suiteVersionString = SuiteVersionUtils.GetSuiteVersion();

            if (!fileSystem.DataVersionFileExists(options))
            {
                await fileSystem.WriteDataVersionFile(options, suiteVersionString, cancellationToken);
                logger.Information("Initialized data version to '{SuiteVersion}'", suiteVersionString);
                return null;
            }

            var persistedVersionString = await fileSystem.ReadDataVersionFile(options, cancellationToken);
            if (string.IsNullOrWhiteSpace(persistedVersionString))
            {
                // should never happen but for sanity we need try to fix it to current version
                await fileSystem.WriteDataVersionFile(options, suiteVersionString, cancellationToken);
                logger.Warning("Empty data version restored to '{SuiteVersion}'", suiteVersionString);
                throw new InvalidOperationException($"Empty data version. Try restore to {suiteVersionString}");
            }

            var suiteVersion = SemVersion.Parse(suiteVersionString);
            var persistedVersion = SemVersion.Parse(persistedVersionString);
            var compareResult = SemVersion.ComparePrecedence(suiteVersion, persistedVersion);
            if (compareResult == 0)
                return null;

            // is persisted version higher than running one?
            if (compareResult > 0)
            {
                await fileSystem.WriteDataVersionFile(options, suiteVersionString, cancellationToken);
                logger.Information("Updated data version to '{SuiteVersion}'", suiteVersionString);
                return null;
            }

            // if we conform to Semver and do our releases that way we could ensure that patch versions would work
            // because a data migration would lead to a minor version change at least
            if (SuiteVersionUtils.IsPatchUpdate(suiteVersion, persistedVersion))
            {
                await fileSystem.WriteDataVersionFile(options, suiteVersionString, cancellationToken);
                logger.Warning("Downgraded data version to '{SuiteVersion}'", suiteVersionString);
                return null;
            }

            logger.Warning("Software version should be updated from '{SuiteVersion}' to at least '{PersistedVersion}'", suiteVersionString, persistedVersionString);

            return new VersionDowngradeInformation(suiteVersionString, persistedVersionString);
        }
    }
}
