using System.Diagnostics;
using Blazor.Shared.Module.ControlPanels;
using Core.Shared.Modules.Contracts;
using Sdk.Modules;

namespace Blazor.Shared.Module.Models;

/// <summary>
/// UI wrapper for <see cref="ModuleMetadataBundle"/> 
/// </summary>
[DebuggerDisplay("Name = {Name,nq}, Version = {Version,nq}, Installed = {Installed,nq}")]
internal sealed class ModuleMetadataModel
{
    public string ModuleId => Bundle.ModuleId;

    public string Name => Bundle.Metadata.Name;

    public string? Title => Bundle.Metadata.Title;

    public string? Description => Bundle.Metadata.Description;

    public string Version => Bundle.Metadata.Version;

    public required ModuleMetadataBundle Bundle { get; set; }

    public bool Installed { get; set; }

    public bool ToBeInstalled { get; set; }

    public required Dictionary<string, ModuleOptionDeclaration> EditOptions { get; set; }

    public List<ModuleDependencyPackage> Dependencies => Bundle.Metadata.Dependencies ?? [];

    public List<ModuleDependencyPackage> MissingDependencies => Bundle.MissingDependencies ?? [];

    public string? SelectedVersion { get; set; }

    public bool CanBeModified => Bundle.CanBeModified;

    public bool CanUpdate => Bundle.CanUpdate;

    public bool HasError => Bundle.Errors.Count > 0;

    public string? UpdateVersion { get; set; }

    public List<string> AvailableVersions => Bundle.AvailableVersions;

    public bool OptionsExpanded { get; set; }

    public bool HasModifiedOptions { get; set; }

    public HashSet<ModuleOptionDeclaration> CustomOptions { get; } = [];

    public IModuleOptionDeclarationCollectionGrid? OptionGrid { get; set; }
}
