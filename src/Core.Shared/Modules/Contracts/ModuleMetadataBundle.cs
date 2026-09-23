using System.Diagnostics;
using Sdk.Messaging;
using Sdk.Modules;

namespace Core.Shared.Modules.Contracts;

/// <summary>
/// <see cref="ModuleMetadata"/> plus installation and availability state.
/// </summary>
[DebuggerDisplay("ModuleId = {ModuleId,nq}, Version = {Metadata.Version,nq}")]
public sealed class ModuleMetadataBundle
{
    public required string ModuleId { get; init; }

    public required ModuleMetadata Metadata { get; set; }

    /// <summary>
    /// The module is installed and loaded.
    /// </summary>
    public bool Installed { get; set; }

    public List<ModuleDependencyPackage> MissingDependencies { get; set; } = [];

    /// <summary>
    /// When false, the UI cannot install or uninstall the module.
    /// </summary>
    public bool CanBeModified { get; set; } = true;

    /// <summary>
    /// An update is available.
    /// </summary>
    public bool CanUpdate { get; set; }

    /// <summary>
    /// Installable versions; empty when the repository has no compatible version.
    /// </summary>
    public List<string> AvailableVersions { get; set; } = [];

    public ModulePackageOperation? PendingOperation { get; set; }

    /// <summary>
    /// Errors from the module init process, e.g. missing options.
    /// </summary>
    public List<ErrorInfo> Errors { get; set; } = [];

    /// <summary>
    /// The module has a valid *.Backend part.
    /// </summary>
    public bool HasBackend { get; set; }

    /// <summary>
    /// The module has a valid *.Client part.
    /// </summary>
    public bool HasFrontend { get; set; }

    /// <summary>
    /// The module was loaded from a debug path.
    /// </summary>
    public bool IsDebugSource { get; set; }
}
