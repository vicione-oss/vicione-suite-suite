namespace Blazor.Shared.Module.Models;

/// <param name="Modules">The requested modules.</param>
/// <param name="AllowInstallation">Whether the instance lets the user install, update and uninstall modules.</param>
public sealed record ModuleMetadataResult(List<ModuleMetadataModel> Modules, bool AllowInstallation);
