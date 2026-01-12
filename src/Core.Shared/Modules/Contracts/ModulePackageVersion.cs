using System.Diagnostics;

namespace Core.Shared.Modules.Contracts;

[DebuggerDisplay("{Version} PreRelease:{PrereleaseVersion}")]
public class ModulePackageVersion
{
    public required Version Version { get; set; }

    public string? PrereleaseVersion { get; set; }

    public DateTimeOffset LastModified { get; set; }

    public List<ModulePackagePlatformInfo> Platforms { get; set; } = [];

}
