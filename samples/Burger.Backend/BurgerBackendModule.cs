using Burger.Backend.DbContext;
using Burger.Backend.Services;
using Burger.Backend.StateMachines;
using Burger.Internal;
using Burger.Public.Contracts;
using MassTransit;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Sdk.Backend.Extensions;
using Sdk.Backend.Modules;
using Sdk.Connections;
using Sdk.Connections.Contracts;
using Sdk.Instance;

namespace Burger.Backend;

public sealed class BurgerBackendModule : BackendModule
{
    public override IModuleInitializer ModuleInitializer => new DefaultModuleInitializer<BurgerDbContext>();
    public override void ConfigureServices(IServiceCollection services, IConfiguration config, IMvcBuilder builder)
    {
        services.AddSingleton<IGrill, Grill>();

        services.AddModuleDbContext<IBurgerDbContext, BurgerDbContextSqlite, BurgerDbContextPostgres>(this, enableSynchronization: false);
    }

    public override void ConfigureMessageBus(IServiceCollection busConfig, InstanceType instanceType)
    {
        // Only master and standalone may handle the saga, so registration is skipped otherwise.
        if (instanceType == InstanceType.Slave)
            return;

        // The saga state is persisted while the saga runs; its record is removed once it finishes.
        ((IBusRegistrationConfigurator)busConfig).AddSagaStateMachine<OrderBurgerStateMachine, OrderBurgerState>()
            .EntityFrameworkRepository(r =>
            {
                // https://masstransit-project.com/usage/sagas/efcore.html
                r.ConcurrencyMode = ConcurrencyMode.Optimistic; // Optimistic, requires RowVersion
                r.DatabaseFactory(s => () => (BurgerDbContext)s.GetRequiredService<IBurgerDbContext>());
            });
    }

    public override void UseServices(IApplicationBuilder app)
    {
        var registry = app.ApplicationServices.GetRequiredService<IConnectionTypeRegistry>();
        registry.Register<BurgerConnection, DefaultJsonConnectionSerializer<BurgerConnection>>(Constants.BurgerConnectionType, () => new BurgerConnection(), new(), null);
        base.UseServices(app);
    }
}
