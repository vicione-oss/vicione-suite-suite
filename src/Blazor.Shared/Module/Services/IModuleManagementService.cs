using Blazor.Shared.Module.ControlPanels.Models;
using Blazor.Shared.Module.Models;
using Core.Shared.Modules.Contracts;
using Core.Shared.Modules.Events;
using Sdk.Modules;

namespace Blazor.Shared.Module.Services;

/// <summary>
/// Provides operations for querying and managing module packages and their options.
/// Exposes events for server-side change notifications.
/// </summary>
public interface IModuleManagementService
{
    /// <summary>
    /// Raised when module package operations have been applied on the server side,
    /// forwarded via <see cref="ModulePackageOperationsChanged"/>.
    /// </summary>
    event Func<ModulePackageOperationsChanged, CancellationToken, Task>? OperationsChanged;

    /// <summary>
    /// Raised when module options have been updated on the server side,
    /// forwarded via <see cref="ModuleOptionsChanged"/>.
    /// </summary>
    event Func<ModuleOptionsChanged, CancellationToken, Task>? OptionsChanged;

    /// <summary>
    /// Retrieves the metadata for all available and installed modules.
    /// </summary>
    /// <param name="forceReload">When <see langword="true"/>, bypasses any cache and reloads metadata from the source.</param>    
    Task<List<ModuleMetadataModel>> GetMetadata(bool forceReload = false, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends a command to apply the given package operations and waits for the correlating
    /// <see cref="ModulePackageOperationsChanged"/> event to be consumed, or times out.
    /// </summary>    
    Task<IModuleManagementServiceResult> UpdateOperations(List<ModulePackageOperation> operations, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends a command to update the options of the specified module and waits for the correlating
    /// <see cref="ModuleOptionsChanged"/> event to be consumed, or times out.
    /// Options with a value equal to the environment marker are excluded from the update.
    /// </summary>    
    Task<IModuleManagementServiceResult> UpdateOptions(string moduleId, IEnumerable<ModuleOptionDeclaration> options, CancellationToken cancellationToken = default);
}
