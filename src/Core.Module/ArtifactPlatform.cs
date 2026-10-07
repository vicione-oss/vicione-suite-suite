using System.Runtime.InteropServices;

namespace Core.Module;

internal sealed class ArtifactPlatform(OSPlatform osPlatform, Architecture osArchitecture) : IArtifactPlatform
{
    /// <remarks>
    /// <see cref="RuntimeInformation.RuntimeIdentifier"/> is not used: it reports the RID the runtime was built for, which
    /// differs from the portable RID on distro-built or emulated runtimes, so no module artifact would match.
    /// </remarks>
    public static ArtifactPlatform Current { get; } = new(GetCurrentOSPlatform(), RuntimeInformation.OSArchitecture);

    /// <remarks>
    /// Mapped on access so an unsupported platform only fails the artifact query, not the type initialization.
    /// </remarks>
    public string ModuleRuntimeIdentifier => GetModuleRuntimeIdentifier();

    public string SuitePackageArchitecture => osArchitecture switch
    {
        Architecture.X64 => "amd64",
        Architecture.Arm64 => "arm64",
        _ => throw new PlatformNotSupportedException($"No suite packages are built for architecture '{osArchitecture}'."),
    };

    /// <remarks>
    /// Keep in sync with the <c>RuntimeIdentifiers</c> in <c>Directory.Build.props</c>, which CI builds module packages for.
    /// </remarks>
    private string GetModuleRuntimeIdentifier()
    {
        if (osPlatform == OSPlatform.Linux && osArchitecture == Architecture.X64)
            return "linux-x64";

        if (osPlatform == OSPlatform.Linux && osArchitecture == Architecture.Arm64)
            return "linux-arm64";

        if (osPlatform == OSPlatform.Windows && osArchitecture == Architecture.X64)
            return "win-x64";

        throw new PlatformNotSupportedException($"No module packages are built for platform '{osPlatform}' and architecture '{osArchitecture}'.");
    }

    private static OSPlatform GetCurrentOSPlatform()
    {
        if (OperatingSystem.IsWindows())
            return OSPlatform.Windows;

        if (OperatingSystem.IsLinux())
            return OSPlatform.Linux;

        return OSPlatform.Create(RuntimeInformation.OSDescription);
    }
}
