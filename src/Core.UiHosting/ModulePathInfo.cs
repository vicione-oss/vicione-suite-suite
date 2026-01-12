using System.Diagnostics;

namespace Core.UiHosting;

[DebuggerDisplay("{AssemblyPath,nq}")]
public class ModulePathInfo(string assemblyPath, bool isDebugSource = false) : IEquatable<ModulePathInfo>
{
    public string AssemblyPath { get; init; } = assemblyPath;
    public bool IsDebugSource { get; init; } = isDebugSource;

    public override int GetHashCode() => AssemblyPath.GetHashCode(StringComparison.Ordinal);

    public override bool Equals(object? obj) => obj is ModulePathInfo deps && Equals(deps);
    public bool Equals(ModulePathInfo? other) => Equals(other?.AssemblyPath, AssemblyPath);

    public static bool operator ==(ModulePathInfo left, ModulePathInfo right) => left.Equals(right);

    public static bool operator !=(ModulePathInfo left, ModulePathInfo right) => !(left == right);
}
