namespace Core.Module.Options;

[System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA2227:Collection properties should be read only", Justification = "Setter properties are required for deserialisation")]
public sealed class ModuleLoaderOptions
{
    public const string ConfigSection = "ModuleLoader";

    /// <summary>
    /// When false, the user cannot install or uninstall modules.
    /// </summary>
    public bool AllowInstallation { get; set; } = true;

    /// <summary>
    /// Manifest json seeded on startup when appdata holds no manifest of its own.
    /// </summary>
    public string? ManifestSeedPath { get; set; }

    /// <summary>
    /// Folder to load published modules from.
    /// </summary>
    public string? ModulesPath { get; set; }

    /// <summary>
    /// Folders to load modules to debug.
    /// </summary>
    public List<string>? ModuleDebugPaths { get; set; }

    /// <summary>
    /// Folders to exclude from the module search.
    /// </summary>
    public List<string> ExcludedPathParts { get; set; } =
    [
        "AppData",
        "Benchmarks",
    ];

    /// <summary>
    /// When set, Core.OS writes the assembly dependency mapping as json to this path.
    /// </summary>
    public string? DumpMappingFilePath { get; set; }

    /// <summary>
    /// ModuleId of the ui host to load.
    /// </summary>
    public string? UiHost { get; set; }

    public string? UiHostsPath { get; set; } = "UiHosts";


    /// <summary>
    /// Checks a module in a MetadataLoadContext before it enters the suite's assembly load context.
    /// </summary>
    public bool UseTypeValidation { get; set; } = true;
}
