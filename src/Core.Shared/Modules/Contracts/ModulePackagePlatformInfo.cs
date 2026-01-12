using System.Diagnostics;

namespace Core.Shared.Modules.Contracts;

[DebuggerDisplay("{Platform} {Architecture}")]
public class ModulePackagePlatformInfo
{
    public required string Platform { get; set; }

    public required string Architecture { get; set; }

    public Uri? DownloadUrl { get; set; }
}
