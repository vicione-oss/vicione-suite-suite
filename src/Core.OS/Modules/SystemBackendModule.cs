using Sdk.Backend.Modules;

namespace Core.OS.Modules;

/// <summary>
/// marker class for the db contexts - try to get rid of it
/// </summary>
public sealed class SystemBackendModule : BackendModule
{
    public override void ConfigureServices(IServiceCollection services, IConfiguration config, IMvcBuilder builder) { }

    public override void UseServices(IApplicationBuilder app) { }

    public override void MapEndpoints(IEndpointRouteBuilder endpoints) { }
}
