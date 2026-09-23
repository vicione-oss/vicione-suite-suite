using Core.Module.Utils;
using Sdk.Backend.Artifacts;
using Semver;

namespace Core.Module.Extensions;

internal static class IArtifactQueryResultExtensions
{
    extension(IArtifactQueryResult queryResult)
    {
        public IEnumerable<IArtifact> OrderModuleArtifactsByVersionDesc(Func<SemVersion, bool>? where = null)
        {
            // Item names look like:
            // 0.28.0-linux-arm64_0.25.0.json
            // 0.28.1-ci1523472-linux-arm64_0.25.0.json
            // 0.28.0-ci1523472-linux-arm64_0.25.0.json
            // 0.28.0-ci1522372-linux-arm64_0.25.0.json
            // 0.28.0-ci1522372-linux-arm64_0.25.0.json
            return queryResult.Artifacts
                .Select(artifact =>
                {
                    ModuleNameVersionRegex.GetModuleVersion(artifact.Name, out var moduleVersion);
                    return (artifact, moduleVersion);
                })
                .Where(tuple => tuple.moduleVersion != null && (where is null || where.Invoke(tuple.moduleVersion)))
                .OrderByDescending(x => x.moduleVersion!, SemVersion.SortOrderComparer)
                .Select(k => k.artifact);
        }

        public IEnumerable<IArtifact> FilterCompatibleModuleArtifactsBySdkVersion(SemVersion sdkVersion)
        {
            // Item names look like:
            // 0.28.0-linux-arm64_0.25.0.json
            // 0.28.1-ci1523472-linux-arm64_0.25.0.json
            // 0.28.0-ci1523472-linux-arm64_0.25.0.json
            // 0.28.0-ci1522372-linux-arm64_0.25.0.json
            // 0.28.0-ci1522372-linux-arm64_0.25.0.json
            return queryResult.Artifacts
                .Select(artifact =>
                {
                    _ = ModuleNameVersionRegex.TryParse(artifact.Name, out var moduleVersion, out _, out var moduleSdkVersion);
                    return (artifact, moduleVersion, moduleSdkVersion);
                })
                .Where(tuple => tuple.moduleSdkVersion != null && tuple.moduleSdkVersion.ComparePrecedenceTo(sdkVersion) <= 0)
                .OrderByDescending(x => x.moduleVersion!, SemVersion.SortOrderComparer)
                .Select(k => k.artifact);
        }
    }
}
