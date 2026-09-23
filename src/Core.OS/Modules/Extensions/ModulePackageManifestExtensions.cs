using Sdk.Modules;
using Semver;

namespace Core.OS.Modules.Extensions;

internal static partial class ModulePackageManifestExtensions
{
    extension(ModulePackageManifest manifest)
    {
        public ModuleDependencyPackage[] GetValidModulePackages(List<string> moduleIds, Dictionary<string, string> debugModules, ILogger logger)
        {
            var validPackages = new HashSet<ModuleDependencyPackage>();

            foreach (var package in manifest.Packages)
            {
                // Disabled packages are skipped.
                if (!string.IsNullOrEmpty(package.Name) && !moduleIds.Contains(package.Name))
                    continue;

                if (HasMissingDependency(manifest, package, debugModules, logger))
                    continue;

                validPackages.Add(package);
            }

            // A package not enabled by configuration must not be downloaded.
            return [.. validPackages];

            static bool HasMissingDependency(ModulePackageManifest manifest, ModuleDependencyPackage dependencyPackage, Dictionary<string, string> debug, ILogger logger)
            {
                if (dependencyPackage.DependingOn is null)
                    return false;

                // todo - if we debug a module it's not part of the manifest and therefore it won't be resolved
                // as existing dependency

                var hasMissingDependencies = false;
                foreach (var missing in dependencyPackage.DependingOn.Where(dependency => !SupportsDependency(manifest, dependency)))
                {
                    if (!SemVersion.TryParse(missing.Version, out var missingVersion))
                    {
                        LogDisablePackageWithMissingDependency(logger, dependencyPackage.Name, missing.Name, missing.Version);
                        continue;
                    }

                    if (debug.TryGetValue(missing.Name, out var versionString) && SemVersion.TryParse(versionString, out var version))
                    {
                        // Check if debug version satisfies required version
                        hasMissingDependencies |= SemVersion.ComparePrecedence(version, missingVersion) < 0;
                    }
                    else
                    {
                        LogDisablePackageWithMissingVersion(logger, dependencyPackage.Name, missing.Name, missing.Version);
                        hasMissingDependencies = true;
                    }
                }

                return hasMissingDependencies;
            }

            // ci package naming, e.g. 0.28.0-ci1523472
            static bool SupportsDependency(ModulePackageManifest manifest, ModuleDependencyPackage dependency)
            {
                var package = manifest.Packages.FirstOrDefault(p => p.Name == dependency.Name);
                if (package is null)
                    return false;

                if (!SemVersion.TryParse(package.Version, out var packageVersion))
                    return false;

                if (!SemVersion.TryParse(dependency.Version, out var dependencyVersion))
                    return false;

                // A differing major version means breaking changes.
                if (packageVersion.Major != dependencyVersion.Major)
                    return false;

                // The package version has to be at least the dependency version.
                return SemVersion.ComparePrecedence(packageVersion, dependencyVersion) >= 0;
            }
        }

        public List<ModuleDependencyPackage> UpdatePackageVersions(Dictionary<string, SemVersion?> packageVersions)
        {
            foreach (var package in manifest.Packages)
            {
                if (!packageVersions.TryGetValue(package.Name, out var version) || version is null)
                    continue;

                package.Version = version.ToString();

                if (package.DependingOn is null)
                    continue;

                // Dependencies move to the new package version too.
                foreach (var dependency in package.DependingOn)
                {
                    if (packageVersions.TryGetValue(dependency.Name, out var dependencyVersion) && dependencyVersion != null)
                        dependency.Version = dependencyVersion.ToString();
                }
            }

            return [.. manifest.Packages];
        }
    }

    [LoggerMessage(LogLevel.Warning, "Disable module {Package} because dependency {MissingPackage} version '{MissingVersion}' is not a valid semver version.")]
    private static partial void LogDisablePackageWithMissingDependency(ILogger logger, string package, string missingPackage, string missingVersion);

    [LoggerMessage(LogLevel.Warning, "Disable module {Package} because dependency {MissingPackage} version '{MissingVersion}' can't be found.")]
    private static partial void LogDisablePackageWithMissingVersion(ILogger logger, string package, string missingPackage, string missingVersion);
}
