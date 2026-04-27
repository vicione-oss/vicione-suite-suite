using Core.Module.Utils;
using Sdk.Backend.Artifacts;
using Semver;

namespace Core.Module.Extensions;

internal static class IArtifactQueryResultExtensions
{
    public static IEnumerable<IArtifact> OrderModuleArtifactsByVersionDesc(this IArtifactQueryResult queryResult, Func<SemVersion, bool>? where = null)
    {
        // we get a list of items with Name like:
        // 0.28.0-linux-arm64_0.25.0.json
        // 0.28.1-ci1523472-linux-arm64_0.25.0.json
        // 0.28.0-ci1523472-linux-arm64_0.25.0.json
        // 0.28.0-ci1522372-linux-arm64_0.25.0.json
        // 0.28.0-ci1522372-linux-arm64_0.25.0.json
        return queryResult.Artifacts
            .Select(artifact =>
            {
                ModuleNameVersionRegex.GetVersion(artifact.Name, out var moduleVersion);
                return (artifact, moduleVersion);
            })
            .Where(tuple => tuple.moduleVersion != null && (where is null || where.Invoke(tuple.moduleVersion)))
            .OrderByDescending(x => x.moduleVersion!, SemVersion.SortOrderComparer)
            .Select(k => k.artifact);
    }
}
