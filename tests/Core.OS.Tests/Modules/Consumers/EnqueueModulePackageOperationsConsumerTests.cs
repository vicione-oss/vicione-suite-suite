using Core.OS.Instance;
using Core.OS.Modules;
using Core.OS.Modules.Consumers;
using Core.Shared.Modules.Commands;
using Core.Shared.Modules.Contracts;
using Core.Shared.Modules.Events;
using MassTransit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using NSubstitute.ExceptionExtensions;
using Sdk.Instance;
using Sdk.Messaging;
using Sdk.Modules;
using Sdk.Testing.Backend;

namespace Core.OS.Tests.Modules.Consumers;

public class EnqueueModulePackageOperationsConsumerTests
{
    private readonly IModulePackageOperationStore _packageStore = Substitute.For<IModulePackageOperationStore>();

    private Action<IBusRegistrationConfigurator> ConfigureServices(InstanceType type)
        => cfg =>
        {
            cfg.AddConsumer<EnqueueModulePackageOperationsConsumer>();
            cfg.AddSingleton(_packageStore);
            cfg.AddSingleton(Options.Create(new InstanceOptions
            {
                HomeDirectory = "home",
                CacheDirectory = "cache",
                BackupDirectory = "backup",
                Type = type,
            }));
            var infoProvider = Substitute.For<IInstanceInformationProvider>();
            infoProvider.Local.Returns(new TestInstanceInformation
            {
                Id = Guid.NewGuid(),
                Type = type,
                Name = "TestInstance",
                Version = "1.0.0",
            });
            cfg.AddSingleton(infoProvider);
        };

    private static EnqueueModulePackageOperations CreateCommand(params ModulePackageOperation[] operations)
        => new([.. operations]) { InstanceId = Guid.NewGuid() };

    private static ModulePackageOperation Install(string name, string version)
        => new(new ModuleDependencyPackage { Name = name, Version = version }, ModulePackageOperationKind.Install);

    private static ModulePackageOperation Uninstall(string name, string version)
        => new(new ModuleDependencyPackage { Name = name, Version = version }, ModulePackageOperationKind.Uninstall);

    [Fact]
    public async Task Should_consume_command()
    {
        // Arrange
        await using var tester = new MassTransitTester(ConfigureServices(InstanceType.Standalone));
        var command = CreateCommand(Install("TestPackage", "1.0.0"));
        _packageStore.EnqueueOperations(Arg.Any<List<ModulePackageOperation>>(), Arg.Any<CancellationToken>()).Returns([]);

        // Act
        await tester.TestInstanceDependentCommand<EnqueueModulePackageOperations, EnqueueModulePackageOperationsConsumer>(command);

        // Assert
        var consumed = await tester.Harness.Consumed.Any<EnqueueModulePackageOperations>(
            k => k.Context.Message.CorrelationId == command.CorrelationId, TestContext.Current.CancellationToken);
        consumed.Should().BeTrue();
    }

    [Fact]
    public async Task Should_enqueue_operations_in_store()
    {
        // Arrange
        await using var tester = new MassTransitTester(ConfigureServices(InstanceType.Standalone));
        var command = CreateCommand(Install("PackageA", "1.2.3"));
        _packageStore.EnqueueOperations(Arg.Any<List<ModulePackageOperation>>(), Arg.Any<CancellationToken>()).Returns([]);

        // Act
        await tester.TestInstanceDependentCommand<EnqueueModulePackageOperations, EnqueueModulePackageOperationsConsumer>(command);

        // Assert
        await _packageStore.Received(1).EnqueueOperations(
            Arg.Is<List<ModulePackageOperation>>(ops => ops!.Count == 1
                && ops[0].OperationKind == ModulePackageOperationKind.Install
                && ops[0].Package.Name == "PackageA"
                && ops[0].Package.Version == "1.2.3"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Should_publish_enqueued_event_with_instance_id()
    {
        // Arrange: a slave still publishes the per-node event for backend correlation
        await using var tester = new MassTransitTester(ConfigureServices(InstanceType.Slave));
        var changes = new List<ModulePackageChange> { new(CrudAction.Created, Install("Package1", "1.0.0")) };
        var command = CreateCommand(Install("Package1", "1.0.0"));
        _packageStore.EnqueueOperations(Arg.Any<List<ModulePackageOperation>>(), Arg.Any<CancellationToken>()).Returns(changes);

        // Act
        await tester.TestInstanceDependentCommand<EnqueueModulePackageOperations, EnqueueModulePackageOperationsConsumer>(command);

        // Assert
        var published = await tester.Harness.Published.Any<ModulePackageOperationsEnqueued>(
            k => k.Context.Message.InstanceId == command.InstanceId
                && k.Context.Message.CorrelationId == command.CorrelationId
                && k.Context.Message.Changes.Count == changes.Count,
            TestContext.Current.CancellationToken);
        published.Should().BeTrue();
    }

    [Theory]
    [InlineData(InstanceType.Master)]
    [InlineData(InstanceType.Standalone)]
    public async Task Should_forward_change_to_ui_when_not_slave(InstanceType instanceType)
    {
        // Arrange
        await using var tester = new MassTransitTester(ConfigureServices(instanceType));
        var changes = new List<ModulePackageChange>
        {
            new(CrudAction.Created, Install("Package1", "1.0.0")),
            new(CrudAction.Deleted, Uninstall("Package2", "2.1.0"))
        };
        var command = CreateCommand(Install("Package1", "1.0.0"), Uninstall("Package2", "2.1.0"));
        _packageStore.EnqueueOperations(Arg.Any<List<ModulePackageOperation>>(), Arg.Any<CancellationToken>()).Returns(changes);

        // Act
        await tester.TestInstanceDependentCommand<EnqueueModulePackageOperations, EnqueueModulePackageOperationsConsumer>(command);

        // Assert
        var published = await tester.Harness.Published.Any<ModulePackageOperationsChanged>(
            k => k.Context.Message.CorrelationId == command.CorrelationId
                && k.Context.Message.Changes.Count == changes.Count,
            TestContext.Current.CancellationToken);
        published.Should().BeTrue();
    }

    [Fact]
    public async Task Should_not_forward_change_to_ui_when_slave()
    {
        // Arrange
        await using var tester = new MassTransitTester(ConfigureServices(InstanceType.Slave));
        var changes = new List<ModulePackageChange> { new(CrudAction.Created, Install("Package1", "1.0.0")) };
        var command = CreateCommand(Install("Package1", "1.0.0"));
        _packageStore.EnqueueOperations(Arg.Any<List<ModulePackageOperation>>(), Arg.Any<CancellationToken>()).Returns(changes);

        // Act
        await tester.TestInstanceDependentCommand<EnqueueModulePackageOperations, EnqueueModulePackageOperationsConsumer>(command);

        // Assert: the per-node event is published, but the UI-forwarded change is suppressed on a slave
        (await tester.Harness.Published.Any<ModulePackageOperationsEnqueued>(TestContext.Current.CancellationToken)).Should().BeTrue();
        (await tester.Harness.Published.Any<ModulePackageOperationsChanged>(TestContext.Current.CancellationToken)).Should().BeFalse();
    }

    [Fact]
    public async Task Should_fault_on_store_failure()
    {
        // Arrange: ADR-004 (D6) - the consumer rethrows so it is retried and finally dead-lettered
        await using var tester = new MassTransitTester(ConfigureServices(InstanceType.Master));
        var command = CreateCommand(Install("InvalidPackage", "1.0.0"));
        _packageStore.EnqueueOperations(Arg.Any<List<ModulePackageOperation>>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("Failed to queue operations"));

        // Act
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => tester.TestInstanceDependentCommandFault<EnqueueModulePackageOperations, EnqueueModulePackageOperationsConsumer>(command));

        // Assert: the failure feedback is published by the fault consumer, never by the faulted consumer itself
        (await tester.Harness.Published.Any<ModulePackageOperationsEnqueued>(TestContext.Current.CancellationToken)).Should().BeFalse();
        (await tester.Harness.Published.Any<ModulePackageOperationsChanged>(TestContext.Current.CancellationToken)).Should().BeFalse();
    }

    [Fact]
    public async Task Should_handle_empty_operations()
    {
        // Arrange
        await using var tester = new MassTransitTester(ConfigureServices(InstanceType.Standalone));
        var command = CreateCommand();
        _packageStore.EnqueueOperations(Arg.Any<List<ModulePackageOperation>>(), Arg.Any<CancellationToken>()).Returns([]);

        // Act
        await tester.TestInstanceDependentCommand<EnqueueModulePackageOperations, EnqueueModulePackageOperationsConsumer>(command);

        // Assert
        await _packageStore.Received(1).EnqueueOperations(
            Arg.Is<List<ModulePackageOperation>>(ops => ops!.Count == 0), Arg.Any<CancellationToken>());
        (await tester.Harness.Published.Any<ModulePackageOperationsChanged>(
            k => k.Context.Message.Changes.Count == 0, TestContext.Current.CancellationToken)).Should().BeTrue();
    }
}
