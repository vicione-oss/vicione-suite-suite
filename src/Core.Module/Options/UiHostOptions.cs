using Core.Module.Contracts;

namespace Core.Module.Options;

public sealed class UiHostOptions : ModuleOptions
{
    /// <summary>
    /// If enabled DevelopmentFileProvider is used to resolve http asset requests
    /// based on *.staticwebassets.runtime.json information
    /// </summary>
    public bool UseDebugRoot { get; set; }
}
