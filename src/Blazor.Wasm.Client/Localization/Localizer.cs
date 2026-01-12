using Sdk.Client.Modules.Localization;

namespace Blazor.Wasm.Client.Localization
{
    internal sealed class Localizer : IClientModuleLocalizer<Client.BlazorWasmClientModule>
    {
        public string? GetDescription() => BlazorWasmClientModule.Description;
        public string GetTitle() => BlazorWasmClientModule.Title;
    }
}
