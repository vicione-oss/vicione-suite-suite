using Microsoft.Extensions.DependencyInjection;
using Sdk.Client.Modules;
using Sdk.Client.Modules.Localization.Extensions;
using Sdk.Client.NavTiles.Extensions;
using Sdk.Modules;
using TestModule.Client.Localization;

namespace TestModule.Client;

public class TestSomeEditorClientModule : ClientModule
{
    public static string Id => ModuleIdResolver.ResolveId<TestSomeEditorClientModule>();

    public override Action<IServiceCollection> Configure => (services) =>
    {
        services.AddLocalization<TestSomeEditorClientModule, TestLocalizer<TestSomeEditorClientModule>>();
        services.AddNavTiles<TestSomeEditorClientModule>();
    };
}
