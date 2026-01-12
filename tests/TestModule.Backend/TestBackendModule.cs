using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Sdk.Backend.Modules;
using Sdk.Instance;
using Sdk.Modules;

namespace TestModule.Backend;

public class TestBackendModule(IModuleInitializer? initializer) : BackendModule
{
    public static string Id => ModuleIdResolver.ResolveId<TestBackendModule>();
    public override IModuleInitializer? ModuleInitializer { get; } = initializer;

    public event EventHandler<string>? CallReceived;

    public TestBackendModule() : this(null)
    {

    }

    public override string GetResourceDirectory(IServiceProvider services) => "Resources";

    public override void ConfigureServices(IServiceCollection services, IConfiguration config, IMvcBuilder builder)
        => CallReceived?.Invoke(this, nameof(ConfigureServices));

    public override void UseServices(IApplicationBuilder app)
        => CallReceived?.Invoke(this, nameof(UseServices));

    public override void MapEndpoints(IEndpointRouteBuilder endpoints)
        => CallReceived?.Invoke(this, nameof(MapEndpoints));

    public override void ConfigureMessageBus(IServiceCollection busConfig, InstanceType instanceType)
        => CallReceived?.Invoke(this, nameof(ConfigureMessageBus));

    public static string GetAssemblyDll() => $"{GetAssemblyName()}.dll";

    public static string GetAssemblyName() => typeof(TestBackendModule).Assembly!.GetName()!.Name!;
}
