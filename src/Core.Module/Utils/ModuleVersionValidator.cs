using Core.Module.Exceptions;

namespace Core.Module.Utils;

public static class ModuleVersionValidator
{
    public static void ValidateSdkCompatibility(Version sdkVersion, string moduleSdkVersionString)
    {
        if (!Version.TryParse(moduleSdkVersionString, out var moduleSdkVersion))
            throw new SdkIncompatibilityException($"Invalid SDK version {moduleSdkVersionString} reference.");

        if (sdkVersion.Major != moduleSdkVersion.Major
            || sdkVersion.Minor != moduleSdkVersion.Minor)
            throw new SdkIncompatibilityException($"Update ViciOne.Suite.Sdk at least to version {sdkVersion.Major}.{sdkVersion.Minor}.x.");
    }

    public static void ValidateSdkCompatibility(string sdkVersionString, string moduleSdkVersionString)
    {
        if (!Version.TryParse(sdkVersionString, out var sdkVersion))
            throw new SdkIncompatibilityException($"Invalid SDK version {sdkVersionString}");

        ValidateSdkCompatibility(sdkVersion, moduleSdkVersionString);
    }
}
