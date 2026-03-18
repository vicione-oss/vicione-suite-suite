using Core.Module;
using Core.Module.Comparer;
using Core.Shared.HostManagement;
using HostManagement.Shared.Contracts.Network;
using MassTransit;
using Sdk.Backend.Messaging;
using Sdk.Instance;
using Sdk.Messaging;
using Semver;

namespace Core.OS.Instance.Consumers;

public sealed class GetAvailableSuiteVersionsConsumer(
    ISuiteArtifactRepository repository,
    IInstanceInformationProvider instanceInformationProvider,
    ILogger<GetAvailableSuiteVersionsConsumer> logger) : RequestConsumer<GetAvailableSuiteVersions, GetAvailableSuiteVersionsResponse>
{
    protected override async Task<GetAvailableSuiteVersionsResponse> Respond(ConsumeContext<GetAvailableSuiteVersions> context)
    {
        var hostManagementAssemblyName = typeof(NetworkDNSSettings).Assembly.GetName();
        if (hostManagementAssemblyName?.Version is null)
            throw new InvalidOperationException("Failed to get HostManagement version");

        var comparer = new StringVersionComparer();
        var currentVersion = instanceInformationProvider.Local.Version;

        logger.LogInformation("Get available suite artifacts for suite-version='{SuiteVersion}', host-management='{HostMgmtVersion}'",
            currentVersion,
            hostManagementAssemblyName.Version);

        // All suite bundles that are compatible with installed HostManagement
        var suiteBundles = await repository.QuerySuiteArtifactBundles(hostManagementAssemblyName.Version, false, context.CancellationToken);
        var suiteVersions = suiteBundles
            .Where(b => IsVersionCompatible(currentVersion, b.Version))
            .DistinctBy(b => b.Version)
            .Select(b => new SuiteVersionPackage
            {
                Architecture = b.Architecture,
                HostManagementVersion = b.HostManagementVersion ?? new Version(0, 0, 0),
                PackageName = b.Package.Name,
                SignatureName = b.PackageSignature?.Name ?? "missing",
                Version = b.Version,
                Installed = string.CompareOrdinal(b.Version, currentVersion) == 0
            }).ToList();

        // If current version can't be downloaded anymore we have to add it manually
        if (suiteVersions.All(b => !b.Installed))
        {
            suiteVersions.Add(new SuiteVersionPackage
            {
                Architecture = repository.GetOSArchitectureFilter(),
                HostManagementVersion = hostManagementAssemblyName.Version,
                PackageName = "vicione-suite",
                SignatureName = "installed",
                Version = currentVersion,
                Installed = true,
            });
        }

        // Sort the list by real version instead of string        
        suiteVersions.Sort((x, y) => comparer.Compare(x.Version, y.Version));

        return new GetAvailableSuiteVersionsResponse(suiteVersions);
    }

    private static bool IsVersionCompatible(string currentVersionString, string packageVersionString)
    {
        // Our installed suite version will always seem to be a tagged version because it's taken
        // from the version file like 1.1.0
        if (!SemVersion.TryParse(currentVersionString, out var currentVersion) ||
            !SemVersion.TryParse(packageVersionString, out var packageVersion))
            return false;

        // After installing a ci version like 1.1.0~1231231 the current version will be again 1.1.0
        // therefore we can't use the normal version check here but we need to ensure that no lower
        // minor version gets installed        
        if (!packageVersion.IsPrerelease)
            return currentVersion.ComparePrecedenceTo(packageVersion) <= 0;

        // Compare only the full version here!!
        var fakeRelease = new SemVersion(packageVersion.Major, packageVersion.Minor, packageVersion.Patch);
        return currentVersion.ComparePrecedenceTo(fakeRelease) <= 0;
    }

    protected override Task<GetAvailableSuiteVersionsResponse> HandleException(ConsumeContext<GetAvailableSuiteVersions> context,
        Exception e)
        => Task.FromResult(new GetAvailableSuiteVersionsResponse([]) { RequestError = new ErrorInfo(0, e.Message) });
}
