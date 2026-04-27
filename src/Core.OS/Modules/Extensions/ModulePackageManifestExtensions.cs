using Sdk.Modules;
using Semver;

namespace Core.OS.Modules.Extensions;

internal static class ModulePackageManifestExtensions
{
    extension(ModulePackageManifest manifest)
    {
        public ModuleDependencyPackage[] GetValidModulePackages(List<string> moduleIds, Dictionary<string, string> debugModules, Serilog.ILogger? logger)
        {
            var validPackages = new HashSet<ModuleDependencyPackage>();

            foreach (var package in manifest.Packages)
            {
                // skip disabled packages
                if (!string.IsNullOrEmpty(package.Name) && !moduleIds.Contains(package.Name))
                    continue;

                // check dependencies
                if (HasMissingDependency(manifest, package, debugModules, logger))
                    continue;

                validPackages.Add(package);
            }

            // prevent downloading packages that are not enabled by configuration
            return [.. validPackages];

            static bool HasMissingDependency(ModulePackageManifest manifest, ModuleDependencyPackage dependencyPackage, Dictionary<string, string> debug, Serilog.ILogger? logger)
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
                        logger?.Warning("Disable module {Package} because dependency {MissingName} version '{MissingVersion}' is not a valid semver version.",
                            dependencyPackage.Name, missing.Name, missing.Version);
                        continue;
                    }

                    if (debug.TryGetValue(missing.Name, out var versionString) && SemVersion.TryParse(versionString, out var version))
                    {
                        // Check if debug version satisfies required version
                        hasMissingDependencies = SemVersion.ComparePrecedence(version, missingVersion) < 0;
                    }
                    else
                    {
                        logger?.Warning("Disable module {Package} because dependency {MissingName} version '{MissingVersion}' can't be found.",
                            dependencyPackage.Name, missing.Name, missing.Version);
                        hasMissingDependencies = true;
                    }
                }

                return hasMissingDependencies;
            }

            // ci package naming like 0.28.0-ci1523472
            static bool SupportsDependency(ModulePackageManifest manifest, ModuleDependencyPackage dependency)
            {
                var package = manifest.Packages.FirstOrDefault(p => p.Name == dependency.Name);
                if (package is null)
                    return false;

                if (!SemVersion.TryParse(package.Version, out var packageVersion))
                    return false;

                if (!SemVersion.TryParse(dependency.Version, out var dependencyVersion))
                    return false;

                // major version difference contains breaking changes
                if (packageVersion.Major != dependencyVersion.Major)
                    return false;
                
                // check if the package version is equal or higher than the dependency version
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

                // also update dependencies to new package version
                foreach (var dependency in package.DependingOn)
                {
                    if (packageVersions.TryGetValue(package.Name, out var dependencyVersion) && dependencyVersion != null)
                        dependency.Version = dependencyVersion.ToString();
                }
            }

            return [.. manifest.Packages];
        }
    }
}
