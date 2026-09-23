using System.IO.Abstractions;
using Core.OS.Hosting.Contracts;
using Core.OS.Instance;
using Core.OS.Instance.Extensions;
using Semver;

namespace Core.OS.Hosting.Extensions;

internal static partial class IFileSystemExtensions
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

            // Adequate for a short string.
            return fileSystem.File.WriteAllTextAsync(dataVersionPath, version, cancellationToken);
        }

        public Task<string> ReadDataVersionFile(InstanceOptions options, CancellationToken cancellationToken = default)
        {
            var dataVersionPath = fileSystem.GetLocalDataVersionFilePath(options);

            // Adequate for a short string.
            return fileSystem.File.ReadAllTextAsync(dataVersionPath, cancellationToken);
        }

        /// <summary>
        /// Compares the running suite version against the data version persisted on disk.
        /// </summary>
        /// </list>
        /// An <see cref="InvalidOperationException"/> is thrown if the persisted version file exists but is empty,
        /// after restoring the file to the current version.
        /// </remarks>
        /// <returns>
        /// <see langword="null"/> when no downgrade action is required;
        /// otherwise a <see cref="VersionDowngradeInformation"/> describing the current and persisted versions.
        /// </returns>
        public async Task<VersionDowngradeInformation?> DetectVersionDowngrade(InstanceOptions options, ILogger logger, CancellationToken cancellationToken = default)
        {
            var suiteVersionString = SuiteVersionUtils.GetSuiteVersion();

            if (!fileSystem.DataVersionFileExists(options))
            {
                await fileSystem.WriteDataVersionFile(options, suiteVersionString, cancellationToken);
                LogDataVersionInitialized(logger, suiteVersionString);
                return null;
            }

            var persistedVersionString = await fileSystem.ReadDataVersionFile(options, cancellationToken);
            if (string.IsNullOrWhiteSpace(persistedVersionString))
            {
                // Should not happen; the file is restored to the current version as a safeguard.
                await fileSystem.WriteDataVersionFile(options, suiteVersionString, cancellationToken);
                LogDataVersionEmptyRestored(logger, suiteVersionString);
                throw new InvalidOperationException($"Empty data version. Try restore to {suiteVersionString}");
            }

            var suiteVersion = SemVersion.Parse(suiteVersionString);
            var persistedVersion = SemVersion.Parse(persistedVersionString);
            var compareResult = SemVersion.ComparePrecedence(suiteVersion, persistedVersion);
            if (compareResult == 0)
                return null;

            // A persisted version higher than the running one is a downgrade.
            if (compareResult > 0)
            {
                await fileSystem.WriteDataVersionFile(options, suiteVersionString, cancellationToken);
                LogDataVersionUpdated(logger, suiteVersionString);
                return null;
            }

            // Strict Semver releases would let patch versions pass, which is not guaranteed today.
            // because a data migration would lead to a minor version change at least
            if (SuiteVersionUtils.IsPatchUpdate(suiteVersion, persistedVersion))
            {
                await fileSystem.WriteDataVersionFile(options, suiteVersionString, cancellationToken);
                LogDataVersionDowngraded(logger, suiteVersionString);
                return null;
            }

            LogDataVersionDowngradeDetected(logger, suiteVersionString, persistedVersionString);

            return new VersionDowngradeInformation(suiteVersionString, persistedVersionString);
        }
    }
}
