using Core.OS.Modules;
using Core.OS.Modules.Consumers;
using Core.Shared.Modules.Commands;
using Core.Shared.Modules.Contracts;
using Core.Shared.Modules.Events;
using AwesomeAssertions;
using MassTransit;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Sdk.Messaging;
using Sdk.Modules;
using Sdk.Testing.Backend;
using Xunit;

namespace Core.OS.Tests.Modules.Consumers;

public class UpdateModulePackageOperationsConsumerTests
{
    private readonly Action<IBusRegistrationConfigurator> _configureServices;
    private readonly IModulePackageOperationStore _packageStore = Substitute.For<IModulePackageOperationStore>();

    public UpdateModulePackageOperationsConsumerTests()
    {
        _configureServices = cfg =>
        {
            // consumer needs
            cfg.AddConsumer<UpdateModulePackageOperationsConsumer>();
            cfg.AddSingleton(_packageStore);
        };
    }

    [Fact]
    public async Task Should_consume_command()
    {
        // Arrange
        await using var tester = new MassTransitTester(_configureServices);
        var operations = new List<ModulePackageOperation>
        {
            new(new ModuleDependencyPackage { Name = "TestPackage", Version = "1.0.0" }, ModulePackageOperationKind.Install)
        };
        var command = new UpdateModulePackageOperations(operations);

        _packageStore.EnqueueOperations(Arg.Any<List<ModulePackageOperation>>(), Arg.Any<CancellationToken>())
            .Returns([]);

        // Act
        await tester.TestCommand<UpdateModulePackageOperations, UpdateModulePackageOperationsConsumer>(command);

        // Assert
        var consumed = await tester.Harness.Consumed.Any<UpdateModulePackageOperations>(k
            => k.Context.Message.CorrelationId == command.CorrelationId, TestContext.Current.CancellationToken);
        consumed.Should().BeTrue();
    }

    [Fact]
    public async Task Should_publish_success_event_with_operations()
    {
        // Arrange
        await using var tester = new MassTransitTester(_configureServices);

        var installOperation = new ModulePackageOperation(new ModuleDependencyPackage
        {
            Name = "Package1",
            Version = "1.0.0"
        }, ModulePackageOperationKind.Install);

        var uninstallOperation = new ModulePackageOperation(new ModuleDependencyPackage
        {
            Name = "Package2",
            Version = "2.1.0"
        }, ModulePackageOperationKind.Uninstall);

        var changes = new List<ModulePackageChange>
        {
            new(CrudAction.Created, installOperation),
            new(CrudAction.Deleted, uninstallOperation)
        };
        var command = new UpdateModulePackageOperations([installOperation, uninstallOperation]);

        _packageStore.EnqueueOperations(Arg.Any<List<ModulePackageOperation>>(), Arg.Any<CancellationToken>())
            .Returns(changes);

        // Act
        await tester.TestCommand<UpdateModulePackageOperations, UpdateModulePackageOperationsConsumer>(command);

        // Assert: Dependencies updated event published with correct operations
        var published = await tester.Harness.Published.Any<ModulePackageOperationsChanged>(k
            => k.Context.Message.Changes.Count == changes.Count, TestContext.Current.CancellationToken);
        published.Should().BeTrue();
    }

    [Fact]
    public async Task Should_call_package_store_with_correct_operations()
    {
        // Arrange
        await using var tester = new MassTransitTester(_configureServices);
        var operations = new List<ModulePackageOperation>
        {
            new(new ModuleDependencyPackage { Name = "PackageA", Version = "1.2.3" }, ModulePackageOperationKind.Install)
        };
        var command = new UpdateModulePackageOperations(operations);

        _packageStore.EnqueueOperations(Arg.Any<List<ModulePackageOperation>>(), Arg.Any<CancellationToken>())
            .Returns([]);

        // Act
        await tester.TestCommand<UpdateModulePackageOperations, UpdateModulePackageOperationsConsumer>(command);

        // Assert: Verify the dependency store was called with the correct operations
        await _packageStore.Received(1).EnqueueOperations(
            Arg.Is<List<ModulePackageOperation>>(ops => ops.Count == operations.Count
                && ops[0].OperationKind == operations[0].OperationKind
                && ops[0].Package.Name == operations[0].Package.Name
                && ops[0].Package.Version == operations[0].Package.Version),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Should_fault_on_queue_error_and_publish_error_event()
    {
        // Arrange
        await using var tester = new MassTransitTester(_configureServices);
        var operations = new List<ModulePackageOperation>
        {
            new(new ModuleDependencyPackage { Name = "InvalidPackage", Version = "1.0.0" }, ModulePackageOperationKind.Install)
        };
        var command = new UpdateModulePackageOperations(operations);

        _packageStore.EnqueueOperations(Arg.Any<List<ModulePackageOperation>>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("Failed to queue operations"));

        // Act
        await tester.TestCommand<UpdateModulePackageOperations, UpdateModulePackageOperationsConsumer>(command);

        // Assert        
        var published = await tester.Harness.Published.Any<ModulePackageOperationsChanged>(k
            => k.Context.Message.CorrelationId == command.CorrelationId
            && k.Context.Message.Error is not null
            && k.Context.Message.Error.ErrorCode == 230, TestContext.Current.CancellationToken);
        published.Should().BeTrue();
    }

    [Fact]
    public async Task Should_handle_empty_operations_list()
    {
        // Arrange
        await using var tester = new MassTransitTester(_configureServices);
        var operations = new List<ModulePackageOperation>();
        var command = new UpdateModulePackageOperations(operations);

        _packageStore.EnqueueOperations(Arg.Any<List<ModulePackageOperation>>(), Arg.Any<CancellationToken>())
            .Returns([]);

        // Act
        await tester.TestCommand<UpdateModulePackageOperations, UpdateModulePackageOperationsConsumer>(command);

        // Assert: Empty operations should still succeed
        var published = await tester.Harness.Published.Any<ModulePackageOperationsChanged>(k
            => k.Context.Message.Changes.Count == 0, TestContext.Current.CancellationToken);
        published.Should().BeTrue();

        await _packageStore.Received(1).EnqueueOperations(
            Arg.Is<List<ModulePackageOperation>>(ops => ops.Count == 0),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Should_handle_multiple_install_operations()
    {
        // Arrange
        await using var tester = new MassTransitTester(_configureServices);

        var installOperation1 = new ModulePackageOperation(new ModuleDependencyPackage
        {
            Name = "Package1",
            Version = "1.0.0"
        }, ModulePackageOperationKind.Install);

        var installOperation2 = new ModulePackageOperation(new ModuleDependencyPackage
        {
            Name = "Package2",
            Version = "2.1.0"
        }, ModulePackageOperationKind.Install);

        var installOperation3 = new ModulePackageOperation(new ModuleDependencyPackage
        {
            Name = "Package3",
            Version = "3.4.80"
        }, ModulePackageOperationKind.Install);

        var changes = new List<ModulePackageChange>
        {
            new(CrudAction.Created, installOperation1),
            new(CrudAction.Created, installOperation2),
            new(CrudAction.Created, installOperation3)
        };

        var command = new UpdateModulePackageOperations([installOperation1, installOperation2, installOperation3]);

        _packageStore.EnqueueOperations(Arg.Any<List<ModulePackageOperation>>(), Arg.Any<CancellationToken>())
            .Returns(changes);

        // Act
        await tester.TestCommand<UpdateModulePackageOperations, UpdateModulePackageOperationsConsumer>(command);

        // Assert: All operations should be queued
        await _packageStore.Received(1).EnqueueOperations(
            Arg.Is<List<ModulePackageOperation>>(ops => ops.Count == changes.Count),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Should_handle_mixed_install_and_uninstall_operations()
    {
        // Arrange
        await using var tester = new MassTransitTester(_configureServices);
        var installOperationA = new ModulePackageOperation(new ModuleDependencyPackage
        {
            Name = "PackageA",
            Version = "1.0.0"
        }, ModulePackageOperationKind.Install);

        var uninstallOperationB = new ModulePackageOperation(new ModuleDependencyPackage
        {
            Name = "PackageB",
            Version = "2.1.0"
        }, ModulePackageOperationKind.Uninstall);

        var installOperationC = new ModulePackageOperation(new ModuleDependencyPackage
        {
            Name = "PackageC",
            Version = "3.4.80"
        }, ModulePackageOperationKind.Install);

        var changes = new List<ModulePackageChange>
        {
            new(CrudAction.Created, installOperationA),
            new(CrudAction.Created, uninstallOperationB),
            new(CrudAction.Created, installOperationC)
        };
        var command = new UpdateModulePackageOperations([installOperationA, uninstallOperationB, installOperationC]);

        _packageStore.EnqueueOperations(Arg.Any<List<ModulePackageOperation>>(), Arg.Any<CancellationToken>())
            .Returns(changes);

        // Act
        await tester.TestCommand<UpdateModulePackageOperations, UpdateModulePackageOperationsConsumer>(command);

        // Assert: Mixed operations should be handled correctly
        var published = await tester.Harness.Published.Any<ModulePackageOperationsChanged>(k
            => k.Context.Message.Changes.Count == 3
            && k.Context.Message.Changes[0].Operation.OperationKind == ModulePackageOperationKind.Install
            && k.Context.Message.Changes[1].Operation.OperationKind == ModulePackageOperationKind.Uninstall
            && k.Context.Message.Changes[2].Operation.OperationKind == ModulePackageOperationKind.Install, TestContext.Current.CancellationToken);
        published.Should().BeTrue();
    }
}
