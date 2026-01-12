using Sdk.Client.Modules;
using Sdk.Client.Modules.Localization;

namespace TestModule.Client.Localization;

internal class TestLocalizer<TClientModule> : IClientModuleLocalizer<TClientModule>
    where TClientModule : IClientModule
{
    public string? GetDescription() => Common.ModuleDescription;
    public string GetTitle() => Common.ModuleTitle;
}
