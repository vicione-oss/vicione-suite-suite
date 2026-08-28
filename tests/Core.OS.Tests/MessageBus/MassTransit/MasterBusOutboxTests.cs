using Core.Module;
using Core.OS.DbContext;
using Core.OS.Instance;
using Core.OS.Instance.Extensions;
using Core.OS.MessageBus.MassTransit.Configuration;
using Core.OS.Modules;
using Core.OS.Persistence;
using Core.OS.Tests.Extensions;
using Core.Shared.Instance.HealthCheck;
using MassTransit;
using MassTransit.DependencyInjection;
using MassTransit.EntityFrameworkCoreIntegration;
using MassTransit.Middleware.Outbox;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Sdk.Backend.Messaging;
using Sdk.Backend.Persistence;
using Sdk.Instance;
using Sdk.Modules;
using Sdk.Testing.Backend;

namespace Core.OS.Tests.MessageBus.MassTransit;

/// <summary>
/// MassTransit's <c>UseBusOutbox()</c> swaps the scoped bus context for one that diverts every send and publish
/// into an <see cref="OutboxDbContext"/> change tracker whenever the scope carries no <c>ConsumeContext</c>.
/// Only replication wants that — it stages into the outbox explicitly via <see cref="IReplicationPublisher"/> —
/// so the master registers the delivery half on its own instead of calling <c>UseBusOutbox()</c>. These tests pin
/// both halves: ordinary traffic reaches the transport, and the outbox machinery stays available for the
/// replication path, registered and tuned the way <c>UseBusOutbox()</c> would have left it.
/// </summary>
public class MasterBusOutboxTests
{
    private static readonly TimeSpan TransportAttemptTimeout = TimeSpan.FromSeconds(3);

    private static readonly (Type ServiceType, Type? ImplementationType, ServiceLifetime Lifetime) ScopedBusContextRegistration =
        (typeof(IScopedBusContextProvider<IBus>),
            typeof(EntityFrameworkScopedBusContextProvider<IBus, OutboxDbContext>),
            ServiceLifetime.Scoped);

    [Fact]
    public async Task Should_reach_the_transport_when_sending_outside_a_consumer()
    {
        // Arrange
        await using var serviceProvider = SetupMaster().BuildServiceProvider();
        await using var scope = serviceProvider.CreateAsyncScope();

        // Act
        var busContext = scope.ServiceProvider.GetRequiredService<IScopedBusContextProvider<IBus>>().Context;

        // Assert
        busContext.Should().BeOfType<BusScopedBusContext<IBus>>();
    }

    [Fact]
    public async Task Should_leave_delivery_to_the_consumer_inside_a_consumer()
    {
        // Arrange
        await using var serviceProvider = SetupMaster().BuildServiceProvider();
        await using var scope = serviceProvider.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<Bind<IBus, IScopedConsumeContextProvider>>()
            .Value.PushContext(Substitute.For<ConsumeContext>());

        // Act
        var busContext = scope.ServiceProvider.GetRequiredService<IScopedBusContextProvider<IBus>>().Context;

        // Assert
        busContext.Should().BeOfType<ConsumeContextScopedBusContext>();
    }

    [Fact]
    public async Task Should_not_stage_a_published_event_in_the_bus_outbox()
    {
        // Arrange
        await using var serviceProvider = SetupMaster().BuildServiceProvider();
        await using var scope = serviceProvider.CreateAsyncScope();
        var mediator = scope.ServiceProvider.GetRequiredService<ISuiteMediator>();

        // Act
        await AttemptDelivery(ct => mediator.Publish(CreateChangeSet(), ct));

        // Assert
        scope.ServiceProvider.GetRequiredService<OutboxDbContext>().ChangeTracker.Entries().Should().BeEmpty();
    }

    [Fact]
    public void Should_keep_the_bus_outbox_delivery_service_for_replication()
    {
        // Arrange
        var services = SetupMaster();

        // Act
        var descriptors = services.ToList();

        // Assert
        descriptors.Should().Contain(descriptor => descriptor.ServiceType == typeof(IBusOutboxNotification));
        descriptors.Should().Contain(descriptor => descriptor.ImplementationType == typeof(BusOutboxDeliveryService<OutboxDbContext>));
    }

    [Fact]
    public void Should_register_everything_UseBusOutbox_adds_except_the_scoped_bus_context()
    {
        // Arrange
        var addedByUseBusOutbox = Registrations(SetupBusOutbox(useBusOutbox: true))
            .Except(Registrations(SetupBusOutbox(useBusOutbox: false)))
            .ToList();

        // Act
        var master = Registrations(SetupMaster()).ToHashSet();

        // Assert
        addedByUseBusOutbox.Should().BeEquivalentTo([
            ScopedBusContextRegistration,
            (typeof(IBusOutboxNotification), typeof(BusOutboxNotification), ServiceLifetime.Singleton),
            (typeof(IHostedService), typeof(BusOutboxDeliveryService<OutboxDbContext>), ServiceLifetime.Singleton),
            (typeof(IConfigureOptions<OutboxDeliveryServiceOptions>), null, ServiceLifetime.Singleton)
        ]);
        master.Should().NotContain(ScopedBusContextRegistration);
        master.Should().Contain(addedByUseBusOutbox.Where(registration => registration != ScopedBusContextRegistration));
    }

    [Fact]
    public async Task Should_deliver_the_bus_outbox_with_the_options_UseBusOutbox_configures()
    {
        // Arrange
        await using var stock = SetupBusOutbox(useBusOutbox: true).BuildServiceProvider();
        await using var master = SetupMaster().BuildServiceProvider();

        // Act
        var options = master.GetRequiredService<IOptions<OutboxDeliveryServiceOptions>>().Value;

        // Assert
        options.Should().BeEquivalentTo(stock.GetRequiredService<IOptions<OutboxDeliveryServiceOptions>>().Value);
    }

    [Fact]
    public void Should_stage_replication_in_the_bus_outbox_on_the_master()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddInstanceServices(CreateInstanceOptions(), useInMemoryBus: false);

        // Assert
        services.Should().ContainSingle(descriptor => descriptor.ServiceType == typeof(IReplicationPublisher))
            .Which.ImplementationType.Should().Be<BusOutboxReplicationPublisher>();
    }

    private static DbChangeSet CreateChangeSet()
        => new([new ChangedEntity("{}", "Entity", "Assembly", EntityState.Added)],
            "ContextType",
            1,
            DateTimeOffset.UnixEpoch);

    private static InstanceOptions CreateInstanceOptions()
        => new()
        {
            HomeDirectory = "home",
            CacheDirectory = "cache",
            BackupDirectory = "backup",
            Type = InstanceType.Master
        };

    // The bus is never started here, so a message that correctly goes to the transport never gets
    // delivered. Where the message ends up is what matters, not whether delivery completes.
    private static async Task AttemptDelivery(Func<CancellationToken, Task> deliver)
    {
        using var timeout = new CancellationTokenSource(TransportAttemptTimeout);
        try
        {
            await deliver(timeout.Token);
        }
        catch (OperationCanceledException)
        {
        }
    }

    // Registrations made through a factory or an instance carry no implementation type and therefore compare
    // equal per service type, which is what the delivery options are pinned separately for.
    private static IEnumerable<(Type ServiceType, Type? ImplementationType, ServiceLifetime Lifetime)> Registrations(IServiceCollection services)
        => services.Select(descriptor => (descriptor.ServiceType, descriptor.ImplementationType, descriptor.Lifetime));

    private static IServiceCollection SetupBusOutbox(bool useBusOutbox)
        => new ServiceCollection()
            .AddLogging()
            .AddMassTransit(busConfig =>
            {
                busConfig.AddEntityFrameworkOutbox<OutboxDbContext>(outbox =>
                {
                    outbox.UsePostgres();

                    if (useBusOutbox)
                        outbox.UseBusOutbox();
                });

                busConfig.UsingInMemory();
            });

    private static IServiceCollection SetupMaster()
    {
        var config = new TestConfig()
            .UseInMemoryBus(false)
            .UseInstanceType(InstanceType.Master)
            .BuildConfiguration();

        var moduleHost = Substitute.For<IModuleHost>();
        moduleHost.GetContext()
            .Returns(new SuiteDependencyContext(new ModuleDependencyContext(ModuleType.Backend, "jsonPath", true), null, []));

        var connectionStringProvider = Substitute.For<IMasterDbConnectionStringProvider>();
        connectionStringProvider.ConnectionString
            .Returns("Server=127.0.0.1;Port=5432");

        var masterHealthInfo = Substitute.For<IMasterHealthInfo>();
        masterHealthInfo.IsMasterReachable.Returns(true);

        return new ServiceCollection()
            .AddLogging()
            .AddSingleton(moduleHost)
            .AddSingleton(config)
            .AddSingleton(connectionStringProvider)
            .AddSingleton(masterHealthInfo)
            .AddInstanceServicesMock(InstanceType.Master)
            .AddMassTransitMessageBus(config, _ => { }, []);
    }
}
