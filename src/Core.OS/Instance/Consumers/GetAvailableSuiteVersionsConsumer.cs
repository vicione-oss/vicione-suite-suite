using Core.Artifacts;
using Core.Module;
using Core.Module.Contracts;
using Core.Shared.HostManagement;
using HostManagement.Shared.Contracts.Network;
using Sdk.Backend.Messaging;
using Sdk.Instance;
using Sdk.Messaging;
using Semver;

namespace Core.OS.Instance.Consumers;

public sealed partial class GetAvailableSuiteVersionsConsumer(
    ISuiteArtifactRepository repository,
    IInstanceInformationProvider instanceInformationProvider,
    ILogger<GetAvailableSuiteVersionsConsumer> logger) : RequestConsumer<GetAvailableSuiteVersions, GetAvailableSuiteVersionsResponse>
{
    public override async Task<GetAvailableSuiteVersionsResponse> Respond(GetAvailableSuiteVersions message, CancellationToken cancellationToken)
    {
        var hostManagementAssemblyName = typeof(NetworkDNSSettings).Assembly.GetName();
        if (hostManagementAssemblyName.Version is null)
            throw new InvalidOperationException("Failed to get HostManagement version");

        var currentVersion = SemVersion.Parse(instanceInformationProvider.Local.Version);

        LogConsume(logger, currentVersion, hostManagementAssemblyName.Version);

        // All suite bundles that are compatible with installed HostManagement
        var suiteBundles = await repository.QuerySuiteArtifactBundles(hostManagementAssemblyName.Version, false, cancellationToken);
        var filteredVersions = suiteBundles
            .Where(b => IsVersionCompatible(currentVersion, b.Version))
            .DistinctBy(b => b.Version)
            .ToList();

        // If current version can't be downloaded anymore we have to add it manually
        if (filteredVersions.All(b => b.Version != currentVersion))
        {
            var localPackage = ArtifactRepositoryFactory.CreateArtifact("vicione-suite", "local", "local");
            var localSignature = ArtifactRepositoryFactory.CreateArtifact("vicione-suite-sig", "local", "local");

            filteredVersions.Add(new SuiteArtifactBundle
            {
                Architecture = repository.GetOSArchitectureFilter(),
                Version = currentVersion,
                Package = localPackage,
                PackageSignature = localSignature,
                HostManagementVersion = SemVersion.FromVersion(hostManagementAssemblyName.Version)
            });
        }

        // Sort the list by real version instead of string        
        filteredVersions.Sort((package, versionPackage) => SemVersion.ComparePrecedence(package.Version, versionPackage.Version));

        var suiteVersions = filteredVersions.Select(b => new SuiteVersionPackage
        {
            Architecture = b.Architecture,
            HostManagementVersion = b.HostManagementVersion is null ? "" : b.HostManagementVersion.ToString(),
            PackageName = b.Package.Name,
            SignatureName = b.PackageSignature?.Name ?? "missing",
            Version = b.Version.ToString(),
            Installed = b.Version == currentVersion
        });

        return new GetAvailableSuiteVersionsResponse([.. suiteVersions]);
    }

    private static bool IsVersionCompatible(SemVersion currentVersion, SemVersion packageVersion)
    {
        // After installing a ci version like 1.1.0~1231231 the current version will be again 1.1.0
        // therefore we can't use the normal version check here, but we need to ensure that no lower
        // minor version gets installed        
        if (!packageVersion.IsPrerelease)
            return currentVersion.ComparePrecedenceTo(packageVersion) <= 0;

        // Compare only the full version here!!
        var fakeRelease = new SemVersion(packageVersion.Major, packageVersion.Minor, packageVersion.Patch);
        return currentVersion.ComparePrecedenceTo(fakeRelease) <= 0;
    }

    public override Task<GetAvailableSuiteVersionsResponse> HandleException(GetAvailableSuiteVersions message,
        Exception e, CancellationToken cancellationToken)
        => Task.FromResult(new GetAvailableSuiteVersionsResponse([]) { RequestError = new ErrorInfo(0, e.Message) });

    [LoggerMessage(Level = LogLevel.Information, Message = "Get available suite artifacts for suite='{SuiteVersion}', host-management='{hostManagementVersion}'")]
    private static partial void LogConsume(ILogger<GetAvailableSuiteVersionsConsumer> logger, SemVersion? suiteVersion, Version? hostManagementVersion);
}
