using Microsoft.Extensions.DependencyInjection;
using Sdk.Client.Modules;
using Sdk.Client.Modules.Localization.Extensions;
using Sdk.Client.NavTiles.Extensions;
using Sdk.Modules;
using TestModule.Client.Localization;

namespace TestModule.Client;

public sealed class TestOtherEditorClientModule : ClientModule
{
    public override Action<IServiceCollection, HostingModel> ConfigureServices => (services, _) =>
    {
        services.AddLocalization<TestOtherEditorClientModule, TestLocalizer<TestOtherEditorClientModule>>();
        services.AddNavTiles<TestOtherEditorClientModule>();
    };
}
