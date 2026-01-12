namespace Core.Module.Options;

public class ModuleApiOptions
{
    public PackageApi PackageApi { get; set; }

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

public enum PackageApi
{
    Nexus,
    JFrog
}
