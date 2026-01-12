namespace Blazor.Wasm.Client.Services;

public sealed class ClientModuleLoaderResult
{
    public List<string> LoadedDlls { get; } = [];

    public Dictionary<string, string> LoadErrors { get; } = [];

    public Exception? Error { get; set; }

    public int ZipLength { get; set; }

    public int UnzippedDllCount { get; set; }
}
