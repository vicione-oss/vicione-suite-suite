namespace Core.Module.Options;

[Obsolete("Use ArtifactRepositoryOptions instead - ModuleApiOptions will be removed")]
[System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA2227:Collection properties should be read only", Justification = "Setter properties are required for deserialisation")]
public class ModuleApiOptions
{
    public const string ConfigSection = "ModuleApi";

    /// <summary>
    /// api endpoint providing suite modules
    /// </summary>
    public string Endpoint { get; set; } = string.Empty;

    public string UserName { get; set; } = string.Empty;

    public string Password { get; set; } = string.Empty;

    /// <summary>
    /// Set the livetime of the package cache in ms - after this period the cache gets invalid and
    /// will be refreshed on next request
    /// </summary>
    public long PackageCacheLifetimeMs { get; set; } = 300000;
}

