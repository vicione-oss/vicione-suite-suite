namespace Core.Module.Contracts;

public sealed class ModuleBundleLoadResult<T>
{
    public List<T> Bundles { get; } = [];

    public Dictionary<string, Exception> ErrorDlls { get; } = [];
}
