using Core.OS.Instance;
using Core.OS.Modules;
using Core.OS.Modules.Consumers;
using Core.Shared.Modules;
using Core.Shared.Modules.Commands;
using Core.Shared.Modules.Contracts;
using Core.Shared.Modules.Events;
using MassTransit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using NSubstitute.ExceptionExtensions;
using Sdk.Instance;
using Sdk.Modules;
using Sdk.Testing.Backend;

namespace Core.OS.Tests.Modules.Consumers;

/// <summary>
/// The correlated failure feedback for <see cref="EnqueueModulePackageOperations"/> is published from the fault, not
/// from the faulted consumer. See ADR-004 (D6).
/// </summary>
/// <remarks>
/// <c>UpdateModulePackageOperationsConsumer</c> fans the command out as one copy per instance under a single
/// correlation id, so the single fault consumer on the master sees one fault per failing node. Only the local node's
/// own copy may produce the UI-facing <see cref="ModulePackageOperationsChanged"/>; the per-node
/// <see cref="ModulePackageOperationsEnqueued"/> is published for every fault.
/// </remarks>
public class EnqueueModulePackageOperationsFaultConsumerTests
{
    private readonly IModulePackageOperationStore _packageStore = Substitute.For<IModulePackageOperationStore>();

    [Fact]
    public async Task Should_publish_correlated_error_events_when_the_local_copy_faults()
    {
        // Arrange
        var localInstanceId = Guid.NewGuid();
        await using var tester = new MassTransitTester(ConfigureServices(localInstanceId));
        var command = CreateCommand(localInstanceId);

        // Act
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => tester.TestInstanceDependentCommandFault<EnqueueModulePackageOperations, EnqueueModulePackageOperationsConsumer>(command));

        // Assert
        (await tester.Harness.Consumed.Any<Fault<EnqueueModulePackageOperations>>(TestContext.Current.CancellationToken)).Should().BeTrue();

        (await tester.Harness.Published.Any<ModulePackageOperationsEnqueued>(
            k => k.Context.Message.CorrelationId == command.CorrelationId
                && k.Context.Message.InstanceId == command.InstanceId
                && k.Context.Message.Error is not null
                && k.Context.Message.Error.ErrorCode == ModuleErrorCodes.EnqueueOperationsFailed,
            TestContext.Current.CancellationToken)).Should().BeTrue();

        (await tester.Harness.Published.Any<ModulePackageOperationsChanged>(
            k => k.Context.Message.CorrelationId == command.CorrelationId
                && k.Context.Message.Error is not null
                && k.Context.Message.Error.ErrorCode == ModuleErrorCodes.EnqueueOperationsFailed,
            TestContext.Current.CancellationToken)).Should().BeTrue();
    }

    [Fact]
    public async Task Should_not_publish_a_ui_report_for_another_instances_fault()
    {
        // Arrange: the fan-out gives every node a copy under the same correlation id, so a second node's failure must
        // not put a competing verdict on a correlation the local node reports on. The client completes on whichever
        // verdict arrives first, so a per-node UI report would both duplicate and race.
        var localInstanceId = Guid.NewGuid();
        var otherInstanceId = Guid.NewGuid();
        await using var tester = new MassTransitTester(ConfigureServices(localInstanceId));
        var command = CreateCommand(otherInstanceId);

        // Act
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => tester.TestInstanceDependentCommandFault<EnqueueModulePackageOperations, EnqueueModulePackageOperationsConsumer>(command));

        // Assert
        (await tester.Harness.Published.Any<ModulePackageOperationsEnqueued>(
            k => k.Context.Message.InstanceId == otherInstanceId
                && k.Context.Message.Error is not null,
            TestContext.Current.CancellationToken)).Should().BeTrue("every node's outcome is still reported per node");

        (await tester.Harness.Published.Any<ModulePackageOperationsChanged>(TestContext.Current.CancellationToken))
            .Should().BeFalse("only the local node's own copy may produce the correlated UI report");
    }

    private static EnqueueModulePackageOperations CreateCommand(Guid instanceId)
        => new([
            new ModulePackageOperation(new ModuleDependencyPackage { Name = "TestPackage", Version = "1.0.0" }, ModulePackageOperationKind.Install)
        ])
        { InstanceId = instanceId };

    private Action<IBusRegistrationConfigurator> ConfigureServices(Guid localInstanceId)
        => cfg =>
        {
            cfg.AddConsumer<EnqueueModulePackageOperationsConsumer>();
            cfg.AddConsumer<EnqueueModulePackageOperationsFaultConsumer>();
            cfg.AddSingleton(_packageStore);
            _packageStore.EnqueueOperations(Arg.Any<List<ModulePackageOperation>>(), Arg.Any<CancellationToken>())
                .ThrowsAsync(new InvalidOperationException("Failed to queue operations"));

            cfg.AddSingleton(Options.Create(new InstanceOptions
            {
                HomeDirectory = "home",
                CacheDirectory = "cache",
                BackupDirectory = "backup",
                Type = InstanceType.Master,
            }));
            var infoProvider = Substitute.For<IInstanceInformationProvider>();
            infoProvider.Local.Returns(new TestInstanceInformation
            {
                Id = localInstanceId,
                Type = InstanceType.Master,
                Name = "TestInstance",
                Version = "1.0.0",
            });
            cfg.AddSingleton(infoProvider);
        };
}
