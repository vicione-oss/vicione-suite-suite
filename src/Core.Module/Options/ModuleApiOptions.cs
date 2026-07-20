namespace Core.Module.Options;

// Deprecated: superseded by Core.Artifacts.ArtifactRepositoryOptions. Retained only so the legacy
// "ModuleApi" configuration section keeps working for existing deployments (see ArtifactRepositoryStore).
// Do not use for new configuration; this type will be removed in a future release.
[Obsolete("Use Core.Artifacts.ArtifactRepositoryOptions instead. Kept only for backwards compatibility with the legacy 'ModuleApi' config section and will be removed in a future release.")]
public class ModuleApiOptions
{
    public const string ConfigSection = "ModuleApi";

    /// <summary>
    /// api endpoint providing suite modules
    /// </summary>
    public string Endpoint { get; set; } = string.Empty;

    public string UserName { get; set; } = string.Empty;

    public string Password { get; set; } = string.Empty;
}
