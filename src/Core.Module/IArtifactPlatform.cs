namespace Core.Module;

/// <summary>
/// Platform names that CI uses in artifact names, so a repository can be queried for a platform other than the current one.
/// </summary>
public interface IArtifactPlatform
{
    /// <summary>
    /// Portable RID used in module package names, e.g. <c>linux-arm64</c> in <c>0.28.0-ci1523472-linux-arm64.zip</c>.
    /// </summary>
    /// <exception cref="PlatformNotSupportedException">No module packages are built for the platform or architecture.</exception>
    string ModuleRuntimeIdentifier { get; }

    /// <summary>
    /// Debian architecture used in suite package names, e.g. <c>amd64</c> in <c>vicione-suite_1.0.3_amd64.deb</c>.
    /// </summary>
    /// <exception cref="PlatformNotSupportedException">No suite packages are built for the architecture.</exception>
    string SuitePackageArchitecture { get; }
}
