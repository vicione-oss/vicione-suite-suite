namespace Core.Shared.Modules.Contracts;

public sealed record UnzippedAssemblies(Dictionary<string, byte[]> Dlls, Dictionary<string, byte[]> Pdbs, List<string> Ignored);
