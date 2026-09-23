using Core.Module.Exceptions;
using Semver;

namespace Core.Module.Utils;

public static class ModuleVersionValidator
{
    private const string Sdk = "ViciOne.Suite.Sdk";

    /// <summary>
    /// Validates that a module's SDK version reference is compatible with the running SDK version.
    /// Compatibility requires the same major version and the module must not reference a newer minor or patch version.
    /// </summary>
    /// <exception cref="SdkIncompatibilityException">
    /// Thrown when <paramref name="moduleSdkVersionString"/> is not a valid semantic version string,
    /// when the major versions differ, or when the module targets a newer SDK than the one running.
    /// </exception>
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

    /// <summary>
    /// Validates that a module's SDK version reference is compatible with the running SDK version.
    /// Compatibility requires the same major version and the module must not reference a newer minor or patch version.
    /// </summary>
    /// <exception cref="SdkIncompatibilityException">
    /// Thrown when either version string is not a valid semantic version string,
    /// when the major versions differ, or when the module targets a newer SDK than the one running.
    /// </exception>
    public static void ValidateSdkCompatibility(string sdkVersionString, string moduleSdkVersionString)
    {
        if (!SemVersion.TryParse(sdkVersionString, out var sdkVersion))
            throw new SdkIncompatibilityException($"Invalid {Sdk} version {sdkVersionString}");

        ValidateSdkCompatibility(sdkVersion, moduleSdkVersionString);
    }
}
