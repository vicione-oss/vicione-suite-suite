namespace Core.Module.Options;

[System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA2227:Collection properties should be read only", Justification = "Setter properties are required for deserialisation")]
public sealed class ModuleLoaderOptions
{
    /// <summary>
    /// Prevent user from un-/installing modules if set to false
    /// </summary>
    public bool AllowInstallation { get; set; } = true;

    /// <summary>
    /// If set to true pre-release versions can be installed via module API
    /// </summary>
    public bool AllowPreReleases { get; set; }

    /// <summary>
    /// If the path is set to a valid module manifest json and there's no
    /// module manifest within appdata this file is seeded to be used on startup
    /// </summary>
    public string? ManifestSeedPath { get; set; }

    /// <summary>
    /// Folder to load published modules from
    /// </summary>
    public string? ModulesPath { get; set; }

    /// <summary>
    /// Folders to load modules to debug 
    /// </summary>
    public List<string>? ModuleDebugPaths { get; set; }

    /// <summary>
    /// Folders to exclude from module search
    /// </summary>
    public List<string> ExcludedPathParts { get; set; } =
    [
        "AppData",
        "Benchmarks",
    ];

    /// <summary>
    /// Filepath to instruct Core.OS to write the assembly dependency mapping as json to the path
    /// </summary>
    public string? DumpMappingFilePath { get; set; }

    /// <summary>
    /// Specify ui host that should be loaded by its ModuleId 
    /// </summary>
    public string? UiHost { get; set; }

    public string? UiHostsPath { get; set; } = "UiHosts";


    /// <summary>
    /// Validate if module can be loaded correctly by using the MetadataLoadContext
    /// before loading it into the assembly load context of the suite
    /// </summary>
    public bool UseTypeValidation { get; set; } = true;// keep it for safety
}
