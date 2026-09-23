using System.Diagnostics.CodeAnalysis;
using Sdk.Modules;
using System.Reflection;
using Semver;

namespace Core.Module.Utils;

public static class ModuleHelpers
{
    /// <summary>
    /// Name of the marker file written as the final step of a successful module install.
    /// Its presence signals that download, extraction and metadata write all completed.
    /// A module directory without this marker is considered incomplete/not-installed.
    /// </summary>
    public const string CompletenessMarkerFileName = ".ready";

    public static string DllToDepsJson(string name) => name.Replace(".dll", ".deps.json", StringComparison.OrdinalIgnoreCase);

    public static string DepsJsonToDll(string name) => name.Replace(".deps.json", ".dll", StringComparison.OrdinalIgnoreCase);

    public static string GetNameVersionKey(string name, Version? version) => GetNameVersionKey(name, version?.ToString(3) ?? "unknown");

    public static string GetNameVersionKey(string name, string version) => $"{name}-{version}";

    public static string GetLocalMetadataFileName(string packageName)
        => $"{packageName}.meta.json";

    public static bool IsDllOrPdb(string file, bool pdb)
    {
        if (Equals(Path.GetExtension(file).ToUpperInvariant(), ".DLL"))
            return true;

        if (pdb && Equals(Path.GetExtension(file).ToUpperInvariant(), ".PDB"))
            return true;

        return false;
    }

    public static string? GetFamilyNamePart(string? assemblyName)
        => TryGetFamilyNamePart(assemblyName, out var commonNamePart) ? commonNamePart : null;

    public static bool TryGetFamilyNamePart(string? assemblyName, [NotNullWhen(true)] out string? commonNamePart)
    {
        commonNamePart = null;
        if (string.IsNullOrEmpty(assemblyName))
            return false;

        // Skips libraries such as ViciOne.TreeBuilder.
        if (!(assemblyName.EndsWith(Constants.ModuleSuffixBackend, StringComparison.Ordinal)
            || assemblyName.EndsWith(Constants.ModuleSuffixClient, StringComparison.Ordinal)
            || assemblyName.EndsWith(Constants.ModuleSuffixPublic, StringComparison.Ordinal)
            || assemblyName.EndsWith(Constants.ModuleSuffixInternal, StringComparison.Ordinal)))
            return false;

        // e.g. assemblyName is ViciOne.Suite.XXX.[Internal|Public]
        var split = assemblyName.Split('.');
        if (split.Length <= 1)
            return false;

        // .. ViciOne.Suite.XXX
        commonNamePart = string.Join('.', split.SkipLast(1));
        return true;
    }

    public static AssemblyName GetSdkAssemblyName()
    {
        var sdkAssembly = Assembly.GetAssembly(typeof(ModuleMetadata));
        return sdkAssembly!.GetName() ?? throw new InvalidOperationException("Failed to get SDK assembly");
    }

    public static Version GetSdkAssemblyVersion()
        => GetSdkAssemblyName().Version ?? throw new InvalidOperationException("Failed to get SDK assembly version");

    /// <summary>
    /// Returns the version as Major.Minor.Build.
    /// </summary>
    public static string GetNormalizedVersion(Version version)
        => $"{version.Major}.{version.Minor}.{version.Build}";

    public static string GetNormalizedVersion(SemVersion version)
        => $"{version.Major}.{version.Minor}.{version.Patch}";

    public static string GetNormalizedVersion(Assembly assembly)
    {
        var assemblyName = assembly.GetName();
        var suiteVersion = assemblyName.Version ?? throw new InvalidOperationException($"Version of {assemblyName.Name} can't be determined.");

        return GetNormalizedVersion(suiteVersion);
    }
}
