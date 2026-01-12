using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Sdk.Backend.Modules;
using Sdk.Instance;

namespace TestSystem.Backend;

public class TestSystemModule(IModuleInitializer? initializer) : BackendModule
{
    public const string Id = "TestSystem.Backend";

    public override IModuleInitializer? ModuleInitializer { get; } = initializer;

    public event EventHandler<string>? CallReceived;

    public TestSystemModule() : this(null)
    {

    }

    public override void ConfigureServices(IServiceCollection services, IConfiguration config, IMvcBuilder builder)
    {
        CallReceived?.Invoke(this, nameof(ConfigureServices));
    }

    public override void UseServices(IApplicationBuilder app)
    {
        CallReceived?.Invoke(this, nameof(UseServices));
    }

    public override void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        CallReceived?.Invoke(this, nameof(MapEndpoints));
    }

    public override void ConfigureMessageBus(IServiceCollection busConfig, InstanceType instanceType)
    {
        CallReceived?.Invoke(this, nameof(ConfigureMessageBus));
    }
}
