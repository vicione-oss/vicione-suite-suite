using Core.OS.DbContext;
using Core.OS.Modules.Consumers;
using Core.OS.Tests.Extensions;
using Core.Shared.Modules.Commands;
using Core.Shared.Modules.Contracts;
using Core.Shared.Modules.Events;
using MassTransit;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute.ExceptionExtensions;
using Sdk.Modules;
using Sdk.Testing.Backend;

namespace Core.OS.Tests.Modules.Consumers;

public sealed class UpdateModulePackageOperationsConsumerTests : TestWithDbContextSqlite<ApplicationDbContextSqlite>
{
    private readonly Action<IBusRegistrationConfigurator> _configureServices;

    public UpdateModulePackageOperationsConsumerTests()
        => _configureServices = cfg =>
        {
            cfg.AddConsumer<UpdateModulePackageOperationsConsumer>();
            cfg.AddSingleton<IApplicationDbContext>(_ => TestDbContext);
        };

    [Fact]
    public async Task Should_consume_command()
    {
        // Arrange
        await using var tester = new MassTransitTester(_configureServices);
        var command = new UpdateModulePackageOperations(CreateOperations());

        // Act
        await tester.TestCommand<UpdateModulePackageOperations, UpdateModulePackageOperationsConsumer>(command);

        // Assert
        var consumed = await tester.Harness.Consumed.Any<UpdateModulePackageOperations>(
            k => k.Context.Message.CorrelationId == command.CorrelationId, TestContext.Current.CancellationToken);
        consumed.Should().BeTrue();
    }

    [Fact]
    public async Task Should_dispatch_enqueue_command_to_every_instance()
    {
        // Arrange
        await using var tester = new MassTransitTester(_configureServices);
        var instances = tester.Services.GetRequiredService<IApplicationDbContext>().SeedInstanceInfos(3).ToList();
        var operations = CreateOperations();
        var command = new UpdateModulePackageOperations(operations);

        // Act
        await tester.TestCommand<UpdateModulePackageOperations, UpdateModulePackageOperationsConsumer>(command);

        // Assert: each registered instance receives its own enqueue command with the original correlation and operations
        foreach (var instance in instances)
        {
            var sent = await tester.Harness.Sent.Any<EnqueueModulePackageOperations>(
                k => k.Context.Message.InstanceId == instance.Id
                    && k.Context.Message.CorrelationId == command.CorrelationId
                    && k.Context.Message.Operations.Count == operations.Count,
                TestContext.Current.CancellationToken);
            sent.Should().BeTrue();
        }
    }

    [Fact]
    public async Task Should_not_dispatch_when_no_instances_registered()
    {
        // Arrange
        await using var tester = new MassTransitTester(_configureServices);
        var command = new UpdateModulePackageOperations(CreateOperations());

        // Act
        await tester.TestCommand<UpdateModulePackageOperations, UpdateModulePackageOperationsConsumer>(command);

        // Assert
        var sent = await tester.Harness.Sent.Any<EnqueueModulePackageOperations>(TestContext.Current.CancellationToken);
        sent.Should().BeFalse();
    }

    [Fact]
    public async Task Should_publish_error_event_when_dispatch_fails()
    {
        // Arrange
        var dbContextMock = Substitute.For<IApplicationDbContext>();
        dbContextMock.InstanceInfo.Throws(new InvalidOperationException("boom"));
        Action<IBusRegistrationConfigurator> configureServices = _configureServices + (cfg => cfg.AddSingleton(dbContextMock));

        await using var tester = new MassTransitTester(configureServices);
        var command = new UpdateModulePackageOperations(CreateOperations());

        // Act
        await tester.TestCommand<UpdateModulePackageOperations, UpdateModulePackageOperationsConsumer>(command);

        // Assert
        var published = await tester.Harness.Published.Any<ModulePackageOperationsChanged>(
            k => k.Context.Message.CorrelationId == command.CorrelationId
                && k.Context.Message.Error is not null
                && k.Context.Message.Error.ErrorCode == 231,
            TestContext.Current.CancellationToken);
        published.Should().BeTrue();
    }

    private static List<ModulePackageOperation> CreateOperations()
        => [new(new ModuleDependencyPackage { Name = "TestPackage", Version = "1.0.0" }, ModulePackageOperationKind.Install)];
}
