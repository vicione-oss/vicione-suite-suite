using System.Diagnostics;

namespace Core.Shared.Modules.Contracts;

[DebuggerDisplay("{PackageName}")]
public class ModulePackageInfo
{
    public required string PackageName { get; set; }

    public List<ModulePackageVersion> Versions { get; set; } = [];
}
