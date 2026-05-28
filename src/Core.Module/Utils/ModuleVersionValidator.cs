using Core.Module.Exceptions;
using Semver;

namespace Core.Module.Utils;

public static class ModuleVersionValidator
{
    private const string Sdk = "ViciOne.Suite.Sdk";

    public static void ValidateSdkCompatibility(SemVersion sdkVersion, string moduleSdkVersionString)
    {
        if (!SemVersion.TryParse(moduleSdkVersionString, out var moduleSdkVersion))
            throw new SdkIncompatibilityException($"Invalid module {Sdk} version {moduleSdkVersionString} reference.");

        if (sdkVersion.Major > moduleSdkVersion.Major)
            throw new SdkIncompatibilityException($"Upgrade module {Sdk} to at least {sdkVersion.Major}.0.x.");

        if (sdkVersion.Major < moduleSdkVersion.Major)
            throw new SdkIncompatibilityException($"Downgrade module {Sdk} to {moduleSdkVersion.Major}.0.x.");

        if (moduleSdkVersion.ComparePrecedenceTo(sdkVersion) > 0)
            throw new SdkIncompatibilityException($"Downgrade module {Sdk} to {sdkVersion.Major}.{sdkVersion.Minor}.0 or lower.");
    }

    public static void ValidateSdkCompatibility(string sdkVersionString, string moduleSdkVersionString)
    {
        if (!SemVersion.TryParse(sdkVersionString, out var sdkVersion))
            throw new SdkIncompatibilityException($"Invalid {Sdk} version {sdkVersionString}");

        ValidateSdkCompatibility(sdkVersion, moduleSdkVersionString);
    }
}
