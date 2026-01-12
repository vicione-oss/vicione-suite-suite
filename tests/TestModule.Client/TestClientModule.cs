using System.Security.Claims;
using Microsoft.Extensions.DependencyInjection;
using Sdk.Client.Modules;
using Sdk.Client.Modules.Localization.Extensions;
using Sdk.Client.NavTiles.Extensions;
using Sdk.Modules;
using TestModule.Client.Localization;

namespace TestModule.Client;

public sealed class TestClientModule : ClientModule
{
    public const string ModuleRoute = "/test-client-module";

    public static string Id => ModuleIdResolver.ResolveId<TestClientModule>();

    public List<ClaimsPrincipal> Users { get; } = [];

    public override Func<IServiceProvider, Task> InitializeServices
        => (_) =>
        {
            return Task.CompletedTask;
        };

    public override Func<IServiceProvider, ClaimsPrincipal, Task> OnUserAuthenticated
        => (_, u) =>
        {
            Users.Add(u);
            return Task.CompletedTask;
        };

    public override Action<IServiceCollection, HostingModel> ConfigureServices => (services, _) =>
    {
        services.AddLocalization<TestClientModule, TestLocalizer<TestClientModule>>();
        services.AddNavTiles<TestClientModule>();
    };

    public static string GetAssemblyName() => $"{typeof(TestClientModule).Assembly!.GetName()!.Name}.dll";
}
