using System.IO.Abstractions;
using Core.OS.Hosting.Contracts;
using Core.OS.Hosting.Services;
using Core.OS.HostManagement.Extensions;
using Core.OS.Instance;
using Semver;

namespace Core.OS.Hosting.Extensions;

internal static class WebApplicationBuilderExtensions
{
    public static async Task<bool> DetectVersionDowngrade(this WebApplicationBuilder builder, IFileSystem fileSystem, InstanceOptions options, Serilog.ILogger logger, CancellationToken cancellationToken = default)
    {
        try
        {
            var suiteVersionString = SuiteVersionUtils.GetSuiteVersion();

            if (!fileSystem.DataVersionFileExists(options))
            {
                await fileSystem.WriteDataVersionFile(options, suiteVersionString, cancellationToken);
                logger.Information("Initialized data version to '{SuiteVersion}'", suiteVersionString);
                return false;
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
                return false;

            // is persisted version higher then running one?
            if (compareResult > 0)
            {
                await fileSystem.WriteDataVersionFile(options, suiteVersionString, cancellationToken);
                logger.Information("Updated data version to '{SuiteVersion}'", suiteVersionString);
                return false;
            }

            // if we confirm to Semver and do our releases that way we could ensure that patch versions would work
            // because a data migration would lead to a minor version change at least
            if (SuiteVersionUtils.IsPatchUpdate(suiteVersion, persistedVersion))
            {
                await fileSystem.WriteDataVersionFile(options, suiteVersionString, cancellationToken);
                logger.Warning("Downgraded data version to '{SuiteVersion}'", suiteVersionString);
                return false;
            }

            logger.Warning("Software version should be updated from '{SuiteVersion}' to '{PersistedVersion}'", suiteVersionString, persistedVersionString);

            var hostMgmtOptions = builder.Configuration.GetHostManagementOptions();
            var downgradeOptions = new DowngradeWebApiParameters
            {
                Instance = options,
                HostManagement = hostMgmtOptions,
                Logger = logger,
                SuiteVersion = suiteVersionString,
                PersistedVersion = persistedVersionString
            };

            // run the host what will stop the program workflow here
            await using var host = DowngradeWebApiHostBuilder.Build(builder, fileSystem, downgradeOptions);
            await host.RunAsync(cancellationToken);
        }
        catch (Exception e)
        {
            logger.Error(e, "Error on detecting downgrade");
        }
        return true;
    }


}
