namespace Core.OS.Modules.Contracts;

/// <param name="IncludeInstalled">Returns the bundles of installed modules.</param>
/// <param name="IncludeAvailable">Returns the bundles of modules that are available but not installed.</param>
/// <param name="ForceRefresh">Bypasses the artifact cache when the repository is queried.</param>
/// <param name="ResolveUpdates">Queries the repository for the versions of installed modules without returning available ones.</param>
public record GetModuleMetadataOptions(bool IncludeInstalled, bool IncludeAvailable, bool ForceRefresh = false, bool ResolveUpdates = false);
