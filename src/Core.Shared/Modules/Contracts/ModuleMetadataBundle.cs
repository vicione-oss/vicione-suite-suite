using System.Diagnostics;
using Sdk.Messaging;
using Sdk.Modules;

namespace Core.Shared.Modules.Contracts;

/// <summary>
/// Wrapper for ModuleMetadata with extended information 
/// </summary>
[DebuggerDisplay("ModuleId = {ModuleId,nq}, Version = {Metadata.Version,nq}")]
public sealed class ModuleMetadataBundle
{
    public required string ModuleId { get; init; }

    public required ModuleMetadata Metadata { get; set; }

    /// <summary>
    /// If true the module is already installed and loaded
    /// </summary>
    public bool Installed { get; set; }

    public List<ModuleDependencyPackage> MissingDependencies { get; set; } = [];

    /// <summary>
    /// If the flag is false the module can't be un-/installed by UI 
    /// </summary>
    public bool CanBeModified { get; set; } = true;

    /// <summary>
    /// If true there's an update available for the module
    /// </summary>
    public bool CanUpdate { get; set; }

    public List<string> AvailableVersions { get; set; } = [];

    /// <summary>
    /// List of errors that occured on init process of the module like missing optios etc.
    /// </summary>
    public List<ErrorInfo> Errors { get; set; } = [];

    /// <summary>
    /// If true the module has a valid *.Backend part
    /// </summary>
    public bool HasBackend { get; set; }

    /// <summary>
    /// If true the module has a valid *.Client part
    /// </summary>
    public bool HasFrontend { get; set; }
}
