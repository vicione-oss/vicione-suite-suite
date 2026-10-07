using System.Runtime.InteropServices;

namespace Core.Module.Tests;

/// <summary>
/// Platform combinations for artifact name theories; the platform is passed by name because <see cref="OSPlatform"/> is not
/// serializable as theory data.
/// </summary>
internal static class ArtifactPlatformTheoryData
{
    public static TheoryData<string, Architecture, string> ModulePlatforms => new()
    {
        { "LINUX", Architecture.X64, "linux-x64" },
        { "LINUX", Architecture.Arm64, "linux-arm64" },
        { "WINDOWS", Architecture.X64, "win-x64" },
    };

    public static TheoryData<string, Architecture> UnsupportedModulePlatforms => new()
    {
        { "LINUX", Architecture.X86 },
        { "LINUX", Architecture.Arm },
        { "WINDOWS", Architecture.X86 },
        { "WINDOWS", Architecture.Arm64 },
        { "OSX", Architecture.Arm64 },
        { "FREEBSD", Architecture.X64 },
    };

    public static TheoryData<Architecture, string> SuitePlatforms => new()
    {
        { Architecture.X64, "amd64" },
        { Architecture.Arm64, "arm64" },
    };

    public static TheoryData<Architecture> UnsupportedSuitePlatforms => new()
    {
        Architecture.X86,
        Architecture.Arm,
    };

    public static ArtifactPlatform Create(string osPlatform, Architecture osArchitecture)
        => new(OSPlatform.Create(osPlatform), osArchitecture);
}
