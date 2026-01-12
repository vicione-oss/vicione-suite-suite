namespace Core.OS.Modules;

internal static class ModuleConstants
{
    public const string InitialModulesJsonResourceKey = "Core.OS.Modules.initial-modules.json";
    public const string ModulesFileName = "modules.json";

    /// <summary>
    /// This key is used within modules metadata set by our deployments for modules
    /// where suite should resolve latest version.
    /// </summary>
    public const string LatestVersionKey = "latest";

    public static readonly string[] SampleModuleIds = ["ViciOne.Suite.Burger", "ViciOne.Suite.JiTChat"];
    
    public const string UnknownVersionMarker = "???";
}
