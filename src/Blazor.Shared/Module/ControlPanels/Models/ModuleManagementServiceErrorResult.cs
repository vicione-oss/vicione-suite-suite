namespace Blazor.Shared.Module.ControlPanels.Models;

/// <summary>
/// Describes the result of a failed call to a method of <see cref="IModuleManagementServiceResult"/> 
/// </summary>
public readonly record struct ModuleManagementServiceErrorResult(string ErrorMessage, int? ErrorCode = null)
    : IModuleManagementServiceResult;
