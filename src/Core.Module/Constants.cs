namespace Core.Module;

public static class Constants
{
    public const string WwwRoot = "wwwroot";
    public const string DefaultUiHostsDirectory = "UiHosts";

    public const string SdkBackendModuleTypeName = "Sdk.Backend.Modules.BackendModule";
    public const string SdkClientModuleTypeName = "Sdk.Client.Modules.ClientModule";

    public const string BackendDepsJsonFilter = $"*{ModuleSuffixBackend}.deps.json";
    public const string ClientDepsJsonFilter = $"*{ModuleSuffixClient}.deps.json";

    public const string BlazorServerModuleId = "ViciOne.Suite.Blazor.Server";
    public const string BlazorWasmModuleId = "Blazor.Wasm";

    public const string ModuleSuffixInternal = ".Internal";
    public const string ModuleSuffixPublic = ".Public";
    public const string ModuleSuffixBackend = ".Backend";
    public const string ModuleSuffixClient = ".Client";

    // can only occur on development or error in ci-pipeline
    public const string ModuleCiVersionKey = "__VERSION__";
}
