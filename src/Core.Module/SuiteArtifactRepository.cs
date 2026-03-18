using System.IO.Abstractions;
using System.Runtime.InteropServices;
using Core.Module.Contracts;
using Core.Module.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Sdk.Backend.Artifacts;
using Semver;

namespace Core.Module;

public sealed partial class SuiteArtifactRepository(IArtifactRepository repository,
    IFileSystem fileSystem,
    IOptions<ArtifactRepositoryOptions> options,
    ILogger<SuiteArtifactRepository> logger) : ISuiteArtifactRepository
{
    private const string SuitesBaseFolder = "suites"; // Base folder in the repository
    private const string PackageExtension = ".deb";
    private const string SignatureExtension = ".minisig";

    public async Task<SuitePackageDownloadResult> DownloadAndValidate(string downloadPath, string suiteArtifactName, string signatureArtifactName, CancellationToken cancellationToken)
    {
        if (!SuiteArtifactNameParser.TryParse(suiteArtifactName, out var suiteVersion, out var architecture))
            throw new InvalidOperationException($"Download of {suiteArtifactName} cancelled because artifact name is invalid.");

        var packageArtifact = await QueryArtifact(suiteArtifactName, cancellationToken)
            ?? throw new InvalidOperationException($"Download of {suiteArtifactName} cancelled because artifact can't be found.");

        var suiteBundle = new SuiteArtifactBundle
        {
            Version = suiteVersion,
            Architecture = architecture,
            Package = packageArtifact,
            PackageSignature = await QueryArtifact(signatureArtifactName, cancellationToken)
        };

        return await DownloadAndValidate(downloadPath, suiteBundle, cancellationToken);
    }

    public async Task<SuitePackageDownloadResult> DownloadAndValidate(string downloadPath, SuiteArtifactBundle suiteBundle, CancellationToken cancellationToken)
    {
        if (options.Value.PublicKeys.Count == 0)
            throw new InvalidOperationException($"At least one entry for '{nameof(ArtifactRepositoryOptions.PublicKeys)}' need to be configured.");

        if (suiteBundle.PackageSignature is null)
            throw new InvalidOperationException($"Download of {suiteBundle.Package.Name} cancelled because signature is missing.");

        string? packagePath = null;
        string? signaturePath = null;
        try
        {
            // We need the *.deb + *.deb.minisig file to validate signage
            packagePath = await DownloadArtifact(suiteBundle.Package, downloadPath, cancellationToken);
            signaturePath = await DownloadArtifact(suiteBundle.PackageSignature, downloadPath, cancellationToken);

            var signature = Minisign.Core.LoadSignatureFromFile(signaturePath);
            var signatureIsValid = false;

            // We have multiple public keys and we are satisfied if one succeeds
            foreach (var key in options.Value.PublicKeys)
            {
                // To ingore misconfiguration
                if (string.IsNullOrEmpty(key))
                    continue;

                var publicKey = Minisign.Core.LoadPublicKeyFromString(key);
                if (Minisign.Core.ValidateSignature(packagePath, signature, publicKey))
                {
                    signatureIsValid = true;
                    break;
                }
            }

            // No public key was able to validate our package signature
            if (!signatureIsValid)
                throw new InvalidOperationException($"Suite package '{suiteBundle.Package.Name}' signature is invalid.");
        }
        catch (Exception ex)
        {
            LogFailedToDownloadSuitePackage(logger, ex, suiteBundle.Package.Name);

            // Cleanup suite package when something went wrong
            TryDeleteFile(packagePath);
            TryDeleteFile(signaturePath);

            throw;
        }

        if (!fileSystem.File.Exists(packagePath) || !fileSystem.File.Exists(signaturePath))
            throw new InvalidOperationException($"Download of {suiteBundle.Package.Name} failed.");

        return new SuitePackageDownloadResult(packagePath, signaturePath);
    }

    private async Task<string> DownloadArtifact(IArtifact artifact, string downloadPath, CancellationToken cancellationToken)
    {
        var targetPath = fileSystem.Path.Combine(
        [
            downloadPath,
            artifact.Name,
        ]);

        if (!fileSystem.File.Exists(targetPath))
        {
            await repository.DownloadToFile(artifact, targetPath, cancellationToken);

            LogDownloadedArtifact(logger, artifact.Name, artifact.Size);
        }

        return targetPath;
    }

    private void TryDeleteFile(string? filePath)
    {
        if (string.IsNullOrEmpty(filePath)) return;

        try
        {
            if (fileSystem.File.Exists(filePath))
                fileSystem.File.Delete(filePath);
        }
        catch (Exception ex)
        {
            FailedToDeleteFile(logger, ex, filePath);
        }
    }

    private async Task<IArtifact?> QueryArtifact(string artifactName,
        CancellationToken cancellationToken = default)
    {
        var queryBuilder = repository.CreateQueryBuilder()
            .AndPathMatches(SuitesBaseFolder)
            .AndNameMatches(artifactName);

        // vicione-suite_1.0.0_amd64_1.1.0.deb[.minisig]    
        var aqlQuery = queryBuilder.Build();
        var result = await repository.Query(aqlQuery, cancellationToken);

        return result.Artifacts.FirstOrDefault();
    }

    public async Task<IReadOnlyCollection<IArtifact>> QueryAllSuiteArtifacts(CancellationToken cancellationToken)
    {
        var aqlQuery = repository.CreateQueryBuilder()
            .AndPathMatches($"{SuitesBaseFolder}")
            .OrderByDescending("path", "name")
            .Build();

        var result = await repository.Query(aqlQuery, cancellationToken);

        return result.Artifacts;
    }

    public async Task<IReadOnlyCollection<SuiteArtifactBundle>> QuerySuiteArtifactBundles(Version minimumHostManagementVersion, bool includeUnsignedPackages = false, CancellationToken cancellationToken = default)
    {
        // We have 3 artifacts per suite-version and architecture where only the json metadata contains the hm-version:
        // - vicione-suite_1.0.3_arm64.deb
        // - vicione-suite_1.0.3_arm64.deb.minisig
        // - vicione-suite_1.0.3_arm64_1.1.0.json        
        var aqlQuery = repository.CreateQueryBuilder()
            .AndPathMatches($"{SuitesBaseFolder}")
            .AndNameMatches($"{GetOSArchitectureFilter()}")
            .OrderByDescending("path", "name")
            .Build();

        var minHostMgmtSemVer = SemVersion.FromVersion(minimumHostManagementVersion);

        // Results could contain versions that have a lower HM minor version!
        var result = await repository.Query(aqlQuery, cancellationToken);

        // -> vicione-suite_<suite-version>_<architecture>_<hm-version>.json
        var metadata = result.Artifacts.Where(k => k.Name.EndsWith(".json", StringComparison.Ordinal));
        var bundles = new List<SuiteArtifactBundle>();

        foreach (var item in metadata)
        {
            if (!SuiteArtifactNameParser.TryParseMetadata(item.Name, out var suiteVersion, out var architecture, out var hostManagementVersion))
            {
                LogFailedToParseSuiteArtifactMetadataName(logger, item.Name);
                continue;
            }

            if (minHostMgmtSemVer.ComparePrecedenceTo(SemVersion.FromVersion(hostManagementVersion)) > 0)
            {
                LogHostManagementVersionLowerThanMinimum(logger, hostManagementVersion, minHostMgmtSemVer, item.Name);
                continue;
            }

            // vicione-suite_1.0.3_arm64 -> vicione-suite_1.0.3_arm64.deb[.minisig]            
            var package = TryGetPackage(result.Artifacts, suiteVersion, architecture);
            if (package is null)
            {
                LogFailedToFindSuitePackageFor(logger, item.Name);
                continue;
            }

            var signature = TryGetSignature(result.Artifacts, suiteVersion, architecture);
            if (signature is null)
            {
                LogFailedToFindSuitePackageSignatureFor(logger, item.Name);
            }

            bundles.Add(new SuiteArtifactBundle
            {
                Version = suiteVersion!,
                Architecture = architecture!,
                HostManagementVersion = hostManagementVersion!,
                Package = package,
                PackageSignature = signature,
            });
        }

        return includeUnsignedPackages
            ? bundles
            : [.. bundles.Where(k => k.PackageSignature is not null)];
    }

    public string GetOSArchitectureFilter()
    {
        // We have no packages for Windows so for debugging we take arm64
        // There's no host management on Windows too so we can't break anything
        // return $"*_arm64*";

#pragma warning disable IDE0079 // Remove unnecessary suppression
#pragma warning disable CA1308 // Normalize strings to uppercase
        // We limit to major version and have to filter lower minor versions from result
        // Supported architectures are: amd64, arm64
        return $"*_{RuntimeInformation.OSArchitecture.ToString().ToLowerInvariant()}*";
#pragma warning restore CA1308 // Normalize strings to uppercase
#pragma warning restore IDE0079 // Remove unnecessary suppression
    }

    private static IArtifact? TryGetPackage(IEnumerable<IArtifact> artifacts, string suiteVersion, string architecture)
    {
        var packageVersion = NetCiToJfrog(suiteVersion);
        var packageName = $"vicione-suite_{packageVersion}_{architecture}{PackageExtension}";

        // vicione-suite_1.0.4-ci1234_arm64_1.1.1.json
        return artifacts.FirstOrDefault(k => k.Name == packageName);
    }

    private static IArtifact? TryGetSignature(IEnumerable<IArtifact> artifacts, string suiteVersion, string architecture)
    {
        var packageVersion = NetCiToJfrog(suiteVersion);
        var packageName = $"vicione-suite_{packageVersion}_{architecture}{PackageExtension}{SignatureExtension}";

        // vicione-suite_1.0.4-ci1234_arm64_1.1.1.json
        return artifacts.FirstOrDefault(k => k.Name == packageName);
    }

    private static string NetCiToJfrog(string suiteVersion) => suiteVersion.Replace("-ci", "~", StringComparison.Ordinal);


    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to download suite package {Name}")]
    public static partial void LogFailedToDownloadSuitePackage(ILogger logger, Exception exception, string name);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Downloaded artifact '{Name}', {Size} bytes")]
    public static partial void LogDownloadedArtifact(ILogger logger, string name, long? size = 0);

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to delete '{File}'")]
    public static partial void FailedToDeleteFile(ILogger logger, Exception exception, string file);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Failed to parse suite artifact metadata name '{Name}'")]
    public static partial void LogFailedToParseSuiteArtifactMetadataName(ILogger logger, string name);

    [LoggerMessage(Level = LogLevel.Warning, Message = "HostManagement version '{Version}' is lower than minimum required version '{MinVersion}' for '{Name}'")]
    public static partial void LogHostManagementVersionLowerThanMinimum(ILogger logger, Version version, SemVersion minVersion, string name);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Failed to find suite package for '{Name}'")]
    public static partial void LogFailedToFindSuitePackageFor(ILogger logger, string name);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Failed to find suite package signature for '{Name}'")]
    public static partial void LogFailedToFindSuitePackageSignatureFor(ILogger logger, string name);
}
