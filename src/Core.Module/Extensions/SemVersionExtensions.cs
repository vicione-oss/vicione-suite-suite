using Semver;

namespace Core.Module.Extensions;

public static class SemVersionExtensions
{
    extension(SemVersion version)
    {
        /// <summary>
        /// A pipeline build such as <c>0.24.0-ci1733049</c>; beta and rc prereleases are not CI builds.
        /// </summary>
        public bool IsCiBuild()
            => version.IsPrerelease && version.Prerelease.StartsWith("ci", StringComparison.OrdinalIgnoreCase);
    }
}
