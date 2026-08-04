using Core.OS.DbContext;
using Core.OS.Instance.Consumers;
using Core.OS.Modules;
using Core.Shared.Modules.Commands;
using Core.Shared.Modules.Contracts;
using MassTransit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NSubstitute.ExceptionExtensions;
using Sdk.Modules;

namespace Core.OS.Tests.Instance.Consumers;

public partial class RegisterInstanceConsumerTests
{
    /// <summary>
    /// Covers the master-side module reconciliation sent to a slave once its registration/sync completes.
    /// Exercises <see cref="RegisterInstanceConsumer.SendModuleManifestReconciliation"/> directly because the
    /// surrounding routing-slip completion path is not driven by the in-memory test harness.
    /// </summary>
    public sealed class ModuleReconciliation
    {
        private readonly IModulePackageManifestStore _manifestStore = Substitute.For<IModulePackageManifestStore>();
        private readonly IModulePackageOperationStore _operationStore = Substitute.For<IModulePackageOperationStore>();

        public ModuleReconciliation()
        {
            _manifestStore.Load(Arg.Any<CancellationToken>()).Returns(new ModulePackageManifest { Packages = [] });
            _operationStore.GetEnqueuedOperations(Arg.Any<CancellationToken>()).Returns([]);
        }

        private RegisterInstanceConsumer CreateConsumer()
        {
            var services = new ServiceCollection();
            services.AddSingleton(Substitute.For<IApplicationDbContext>());
            services.AddSingleton(_manifestStore);
            services.AddSingleton(_operationStore);

            return new RegisterInstanceConsumer(services.BuildServiceProvider(), Substitute.For<ILogger<RegisterInstanceConsumer>>());
        }

        private static (ConsumeContext Context, ISendEndpoint Endpoint) CreateContext()
        {
            var context = Substitute.For<ConsumeContext>();
            var endpoint = Substitute.For<ISendEndpoint>();
            context.GetSendEndpoint(Arg.Any<Uri>()).Returns(endpoint);
            return (context, endpoint);
        }

        [Fact]
        public async Task Should_send_reconcile_with_desired_manifest()
        {
            // Arrange
            var instanceId = Guid.NewGuid();
            var correlationId = Guid.NewGuid();
            _manifestStore.Load(Arg.Any<CancellationToken>()).Returns(new ModulePackageManifest
            {
                Packages = [new ModuleDependencyPackage { Name = "ViciOne.Suite.Ping", Version = "1.2.3" }]
            });
            var consumer = CreateConsumer();
            var (context, endpoint) = CreateContext();

            // Act
            await consumer.SendModuleManifestReconciliation(context, instanceId, correlationId, TestContext.Current.CancellationToken);

            // Assert
            await endpoint.Received(1).Send(
                Arg.Is<ReconcileModuleManifest>(m => m!.InstanceId == instanceId
                    && m.CorrelationId == correlationId
                    && m.DesiredPackages.Count == 1
                    && m.DesiredPackages[0].Name == "ViciOne.Suite.Ping"),
                Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task Should_include_masters_pending_operations_in_desired_manifest()
        {
            // Arrange: the master has Ping applied and an Oee install still pending (not yet restarted for)
            var instanceId = Guid.NewGuid();
            _manifestStore.Load(Arg.Any<CancellationToken>()).Returns(new ModulePackageManifest
            {
                Packages = [new ModuleDependencyPackage { Name = "ViciOne.Suite.Ping", Version = "1.0.0" }]
            });
            _operationStore.GetEnqueuedOperations(Arg.Any<CancellationToken>()).Returns(
                [new ModulePackageOperation(new ModuleDependencyPackage { Name = "ViciOne.Suite.Oee", Version = "2.0.0" }, ModulePackageOperationKind.Install)]);
            var consumer = CreateConsumer();
            var (context, endpoint) = CreateContext();

            // Act
            await consumer.SendModuleManifestReconciliation(context, instanceId, Guid.NewGuid(), TestContext.Current.CancellationToken);

            // Assert: desired set combines the applied manifest and the pending install
            await endpoint.Received(1).Send(
                Arg.Is<ReconcileModuleManifest>(m => m!.DesiredPackages.Count == 2
                    && m.DesiredPackages.Any(p => p.Name == "ViciOne.Suite.Ping")
                    && m.DesiredPackages.Any(p => p.Name == "ViciOne.Suite.Oee")),
                Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task Should_not_send_or_throw_when_manifest_load_fails()
        {
            // Arrange
            _manifestStore.Load(Arg.Any<CancellationToken>()).ThrowsAsync(new InvalidOperationException("corrupt manifest"));
            var consumer = CreateConsumer();
            var (context, endpoint) = CreateContext();

            // Act — reconciliation is best-effort and must not fail registration
            await consumer.SendModuleManifestReconciliation(context, Guid.NewGuid(), Guid.NewGuid(), TestContext.Current.CancellationToken);

            // Assert
            await endpoint.DidNotReceive().Send(Arg.Any<ReconcileModuleManifest>(), Arg.Any<CancellationToken>());
        }
    }
}
