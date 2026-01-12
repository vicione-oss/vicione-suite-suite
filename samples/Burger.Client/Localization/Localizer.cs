using Sdk.Client.Modules.Localization;

namespace Burger.Client.Localization;

internal sealed class Localizer : IClientModuleLocalizer<BurgerClientModule>
{
    public string? GetDescription() => Common.ModuleDescription;
    public string GetTitle() => Common.ModuleTitle;
}
