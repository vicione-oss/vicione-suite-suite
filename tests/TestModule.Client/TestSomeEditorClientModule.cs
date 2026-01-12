using Microsoft.Extensions.DependencyInjection;
using Sdk.Client.Modules;
using Sdk.Client.Modules.Localization.Extensions;
using Sdk.Client.NavTiles.Extensions;
using TestModule.Client.Localization;

namespace TestModule.Client;

public class TestSomeEditorClientModule : ClientModule
{
    public override Action<IServiceCollection, HostingModel> ConfigureServices => (services, _) =>
    {
        services.AddLocalization<TestSomeEditorClientModule, TestLocalizer<TestSomeEditorClientModule>>();
        services.AddNavTiles<TestSomeEditorClientModule>();
    };
}
