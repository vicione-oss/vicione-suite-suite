using Sdk.Client.Contracts;
using Sdk.Client.Modules;
using Sdk.Client.Services;

namespace Burger.Client.Services;

public class BurgerClientResourceProvider : IClientModuleResourceProvider
{
    public IEnumerable<Resource> GetResources()
    {
        var stylesheetResource = new Resource
        {
            Bundle = "Burger",
            ResourceType = ResourceType.Stylesheet,
            Url = ModuleAssetHelper.GetModuleCssUrl<BurgerClientModule>("burger-module.css"),
        };

        var jsResource = new Resource
        {
            Bundle = "Burger",
            ResourceType = ResourceType.Script,
            Url = ModuleAssetHelper.GetModuleJsUrl<BurgerClientModule>("burger-module.js"),
        };

        return [stylesheetResource, jsResource];
    }
}
