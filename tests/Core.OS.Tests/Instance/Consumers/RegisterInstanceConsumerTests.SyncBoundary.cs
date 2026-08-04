using Core.OS.DbContext;
using Core.OS.Instance;
using Core.OS.Instance.Commands;
using Core.OS.Instance.Consumers;
using Core.OS.Instance.Services;
using Core.OS.Persistence;
using Core.Shared.Instance.Contracts;
using Core.Shared.Instance.Requests;
using MassTransit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Sdk.Backend.Messaging;
using Sdk.Backend.Persistence;
using Sdk.Instance;
using Sdk.Testing.Backend;

namespace Core.OS.Tests.Instance.Consumers;

public partial class RegisterInstanceConsumerTests
{
    public sealed class SyncBoundary : RegisterInstanceConsumerTests
    {
        private readonly Action<IBusRegistrationConfigurator> _syncBoundaryServices;
        private readonly ReplicationSequenceCounter _sequenceCounter = new();

        public SyncBoundary()
        {
            var mediator = Substitute.For<ISuiteMediator>();
            mediator.Request<GetInstances, GetInstancesResponse>(Arg.Any<GetInstances>(), Arg.Any<CancellationToken>())
                .Returns(new GetInstancesResponse([]));

            _syncBoundaryServices = services =>
            {
                services.AddConsumer<RegisterInstanceConsumer>();
                services.AddSingleton<IApplicationDbContext>(_ => TestDbContext);
                services.AddSingleton(new ModuleContextTypeInformation(Shared.Constants.SystemModuleId, typeof(ApplicationDbContext), typeof(ApplicationDbContext).AssemblyQualifiedName!));
                services.AddRoutingSlipBuilderFactory();
                services.AddSingleton(Substitute.For<ILocalInstanceInformationProvider>());
                services.AddSingleton(new InMemoryClusterInformationProvider(Substitute.For<ILogger<InMemoryClusterInformationProvider>>()));
                services.AddSingleton(mediator);
                services.AddSingleton(Substitute.For<IInstanceConfigurationRepository>());
                services.AddSingleton(new SynchronizationState());
                services.AddSingleton(_sequenceCounter);
            };
        }

        [Fact]
        public async Task Should_trigger_sync_when_slave_registered_exactly_at_queue_lifetime()
        {
            // Arrange
            var instanceId = Guid.NewGuid();
            var queueLifetimeDays = 5;
            TestDbContext.InstanceInfo.Add(new InstanceInformation
            {
                Id = instanceId,
                Type = InstanceType.Slave,
                LastRegistered = DateTimeOffset.Now.AddDays(-queueLifetimeDays),
                InstalledModules = [Shared.Constants.SystemModuleId]
            });
            await TestDbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

            await using var tester = new MassTransitTester(_syncBoundaryServices);
            var command = new RegisterInstance
            {
                Name = "BoundarySlave",
                InstanceId = instanceId,
                Type = InstanceType.Slave,
                InstalledModules = [Shared.Constants.SystemModuleId],
                Configuration =
                [
                    new KeyValuePair<string, string?>("MessageBus:RabbitMq:QueueLifetimeInDays", queueLifetimeDays.ToString(System.Globalization.CultureInfo.InvariantCulture))
                ]
            };

            // Act + Assert
            await tester.TestCommand<RegisterInstance, RegisterInstanceConsumer>(command);
        }

        [Fact]
        public async Task Should_trigger_sync_when_slave_registered_one_second_past_queue_lifetime()
        {
            // Arrange
            var instanceId = Guid.NewGuid();
            var queueLifetimeDays = 5;
            TestDbContext.InstanceInfo.Add(new InstanceInformation
            {
                Id = instanceId,
                Type = InstanceType.Slave,
                LastRegistered = DateTimeOffset.Now.AddDays(-queueLifetimeDays).AddSeconds(-1),
                InstalledModules = [Shared.Constants.SystemModuleId]
            });
            await TestDbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

            await using var tester = new MassTransitTester(_syncBoundaryServices);
            var command = new RegisterInstance
            {
                Name = "PastBoundarySlave",
                InstanceId = instanceId,
                Type = InstanceType.Slave,
                InstalledModules = [Shared.Constants.SystemModuleId],
                Configuration =
                [
                    new KeyValuePair<string, string?>("MessageBus:RabbitMq:QueueLifetimeInDays", queueLifetimeDays.ToString(System.Globalization.CultureInfo.InvariantCulture))
                ]
            };

            // Act + Assert
            await tester.TestCommand<RegisterInstance, RegisterInstanceConsumer>(command);
        }

        [Fact]
        public async Task Should_trigger_sync_when_new_slave_has_null_last_registered()
        {
            // Arrange
            await using var tester = new MassTransitTester(_syncBoundaryServices);
            var command = new RegisterInstance
            {
                Name = "FirstTimeSlave",
                InstanceId = Guid.NewGuid(),
                Type = InstanceType.Slave,
                InstalledModules = [Shared.Constants.SystemModuleId]
            };

            // Act + Assert
            await tester.TestCommand<RegisterInstance, RegisterInstanceConsumer>(command);
        }

        [Fact]
        public async Task Should_bypass_ttl_check_when_force_sync()
        {
            // Arrange
            var instanceId = Guid.NewGuid();
            TestDbContext.InstanceInfo.Add(new InstanceInformation
            {
                Id = instanceId,
                Type = InstanceType.Slave,
                LastRegistered = DateTimeOffset.Now.AddMinutes(-5),
                InstalledModules = [Shared.Constants.SystemModuleId]
            });
            await TestDbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

            await using var tester = new MassTransitTester(_syncBoundaryServices);
            var command = new RegisterInstance
            {
                Name = "ForceSyncSlave",
                InstanceId = instanceId,
                Type = InstanceType.Slave,
                InstalledModules = [Shared.Constants.SystemModuleId],
                ForceSync = true
            };

            // Act + Assert
            await tester.TestCommand<RegisterInstance, RegisterInstanceConsumer>(command);
        }

        [Fact]
        public async Task Should_trigger_sync_when_slave_sequence_ahead_of_master()
        {
            // Arrange
            var instanceId = Guid.NewGuid();
            TestDbContext.InstanceInfo.Add(new InstanceInformation
            {
                Id = instanceId,
                Type = InstanceType.Slave,
                LastRegistered = DateTimeOffset.Now.AddMinutes(-5),
                InstalledModules = [Shared.Constants.SystemModuleId]
            });
            await TestDbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

            _sequenceCounter.Seed("TestContext", 50);

            await using var tester = new MassTransitTester(_syncBoundaryServices);
            var command = new RegisterInstance
            {
                Name = "SequenceMismatchSlave",
                InstanceId = instanceId,
                Type = InstanceType.Slave,
                InstalledModules = [Shared.Constants.SystemModuleId],
                LastAppliedSequences = new Dictionary<string, long> { ["TestContext"] = 100 }
            };

            // Act + Assert
            await tester.TestCommand<RegisterInstance, RegisterInstanceConsumer>(command);
        }

        [Fact]
        public async Task Should_not_trigger_mismatch_when_slave_has_empty_sequences()
        {
            // Arrange
            var instanceId = Guid.NewGuid();
            TestDbContext.InstanceInfo.Add(new InstanceInformation
            {
                Id = instanceId,
                Type = InstanceType.Slave,
                LastRegistered = DateTimeOffset.Now.AddMinutes(-5),
                InstalledModules = [Shared.Constants.SystemModuleId]
            });
            await TestDbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

            _sequenceCounter.Seed("TestContext", 100);

            await using var tester = new MassTransitTester(_syncBoundaryServices);
            var command = new RegisterInstance
            {
                Name = "FreshSlave",
                InstanceId = instanceId,
                Type = InstanceType.Slave,
                InstalledModules = [Shared.Constants.SystemModuleId],
                LastAppliedSequences = []
            };

            // Act + Assert
            await tester.TestCommand<RegisterInstance, RegisterInstanceConsumer>(command);
        }

        [Fact]
        public async Task Should_not_trigger_sync_when_slave_has_matching_sequences()
        {
            // Arrange
            var instanceId = Guid.NewGuid();
            TestDbContext.InstanceInfo.Add(new InstanceInformation
            {
                Id = instanceId,
                Type = InstanceType.Slave,
                LastRegistered = DateTimeOffset.Now.AddMinutes(-5),
                InstalledModules = [Shared.Constants.SystemModuleId]
            });
            await TestDbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

            _sequenceCounter.Seed("TestContext", 50);

            await using var tester = new MassTransitTester(_syncBoundaryServices);
            var command = new RegisterInstance
            {
                Name = "InSyncSlave",
                InstanceId = instanceId,
                Type = InstanceType.Slave,
                InstalledModules = [Shared.Constants.SystemModuleId],
                LastAppliedSequences = new Dictionary<string, long> { ["TestContext"] = 50 }
            };

            // Act + Assert
            await tester.TestCommand<RegisterInstance, RegisterInstanceConsumer>(command);
        }
    }
}
