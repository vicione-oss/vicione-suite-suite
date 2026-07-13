using Core.OS.DbContext;
using Core.OS.Instance;
using Core.OS.Instance.Commands;
using Core.OS.Instance.Consumers;
using Core.OS.Instance.Services;
using Core.OS.Persistence;
using Core.Shared.Instance.Requests;
using MassTransit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Sdk.Backend.Messaging;
using Sdk.Backend.Persistence;
using Sdk.Instance;
using Sdk.Testing.Backend;
using Xunit;

namespace Core.OS.Tests.Instance.Consumers;

public partial class RegisterInstanceConsumerTests : TestWithDbContextSqlite<ApplicationDbContextSqlite>
{
    private readonly Action<IBusRegistrationConfigurator> _configureServices;

    public RegisterInstanceConsumerTests()
    {
        var mediator = Substitute.For<ISuiteMediator>();
        mediator.Request<GetInstances, GetInstancesResponse>(Arg.Any<GetInstances>(), Arg.Any<CancellationToken>())
            .Returns(new GetInstancesResponse([]));

        _configureServices = services =>
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
        };
    }

    [Fact]
    public async Task Should_consume_command_for_standalone_instance()
    {
        // Arrange
        await using var tester = new MassTransitTester(_configureServices);
        var command = new RegisterInstance
        {
            Name = "Test",
            InstanceId = Guid.NewGuid(),
            Type = InstanceType.Standalone
        };

        // Act + Assert
        await tester.TestCommand<RegisterInstance, RegisterInstanceConsumer>(command);
    }

    [Fact]
    public async Task Should_trigger_synchronisation_for_new_slave_instance()
    {
        // Arrange
        await using var tester = new MassTransitTester(_configureServices);
        var command = new RegisterInstance
        {
            Name = "Test",
            InstanceId = Guid.NewGuid(),
            Type = InstanceType.Slave
        };

        command.InstalledModules.Add(Shared.Constants.SystemModuleId);

        // Act + Assert
        await tester.TestCommand<RegisterInstance, RegisterInstanceConsumer>(command);
    }

    [Fact]
    public async Task Should_consume_command_for_slave_instance()
    {
        // Arrange
        await using var tester = new MassTransitTester(_configureServices);
        var command = new RegisterInstance
        {
            Name = "Test",
            InstanceId = Guid.NewGuid(),
            Type = InstanceType.Slave
        };

        // Act + Assert
        await tester.TestCommand<RegisterInstance, RegisterInstanceConsumer>(command);
    }
}
