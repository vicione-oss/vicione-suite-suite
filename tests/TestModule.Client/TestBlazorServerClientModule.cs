using Microsoft.Extensions.DependencyInjection;
using Sdk.Client.Modules;
using Sdk.Client.Modules.Localization.Extensions;
using TestModule.Client.Localization;

namespace TestModule.Client;

public sealed class TestBlazorServerClientModule : ClientModule
{
    public override Action<IServiceCollection> Configure => (services) =>
    {
        services.AddLocalization<TestBlazorServerClientModule, TestLocalizer<TestBlazorServerClientModule>>();
    };
}
