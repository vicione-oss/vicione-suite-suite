using Core.Module.Exceptions;
using Semver;

namespace Core.Module.Utils;

public static class ModuleVersionValidator
{
    public static void ValidateSdkCompatibility(SemVersion sdkVersion, string moduleSdkVersionString)
    {
        if (!SemVersion.TryParse(moduleSdkVersionString, out var moduleSdkVersion))
            throw new SdkIncompatibilityException($"Invalid module SDK version {moduleSdkVersionString} reference.");

        if (sdkVersion.Major != moduleSdkVersion.Major
            || sdkVersion.Minor != moduleSdkVersion.Minor)
            throw new SdkIncompatibilityException($"Update ViciOne.Suite.Sdk at least to version {sdkVersion.Major}.{sdkVersion.Minor}.x.");
    }

    public static void ValidateSdkCompatibility(string sdkVersionString, string moduleSdkVersionString)
    {
        if (!SemVersion.TryParse(sdkVersionString, out var sdkVersion))
            throw new SdkIncompatibilityException($"Invalid SDK version {sdkVersionString}");

        ValidateSdkCompatibility(sdkVersion, moduleSdkVersionString);
    }
}
