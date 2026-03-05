namespace Core.OS.Modules.Contracts;

public record GetModuleMetadataOptions(bool IncludeInstalled, bool IncludeAvailable, bool ForceRefresh = false);
