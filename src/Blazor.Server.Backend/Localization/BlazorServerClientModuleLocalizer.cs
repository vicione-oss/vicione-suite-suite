using Sdk.Client.Modules.Localization;

namespace Blazor.Server.Backend.Localization
{
    internal sealed class BlazorServerClientModuleLocalizer : IClientModuleLocalizer<Backend.BlazorServerClientModule>
    {
        public string? GetDescription() => BlazorServerClientModule.Description;
        public string GetTitle() => BlazorServerClientModule.Title;
    }
}
