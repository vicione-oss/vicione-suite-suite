namespace Core.OS.Modules;

internal static class ModuleConstants
{
    public const string InitialModulesJsonResourceKey = "Core.OS.Modules.initial-modules.json";
    public const string ModulesFileName = "modules.json";
    public const string ModuleOperationsFileName = "modules-ops.json";
    public const string ModuleOperationsSentinelFileName = "modules-ops.sentinel";
    public const string SetByEnvironmentMarker = "<set_by_environment>";
    public const string MetadataFileName = "module-metadata.json";

    /// <summary>
    /// This key is used within modules metadata set by our deployments for modules
    /// where suite should resolve latest version.
    /// </summary>
    public const string LatestVersionKey = "latest";

    public const string UnresolvedVersionMarker = "n/a";

    public static readonly string[] SampleModuleIds = ["ViciOne.Suite.Burger", "ViciOne.Suite.JiTChat"];
}
