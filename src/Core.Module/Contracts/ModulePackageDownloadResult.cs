using Sdk.Modules;

namespace Core.Module.Contracts;

public record ModulePackageDownloadResult(ModuleDependencyPackage Module)
{
    public Exception? Error { get; set; }
    public bool Skipped { get; set; }
}
