namespace Blazor.Shared.Module.Models;

internal readonly record struct ModuleState(string ModuleName, string Version, bool Installed, bool ToBeInstalled, bool ToBeUpdated);
