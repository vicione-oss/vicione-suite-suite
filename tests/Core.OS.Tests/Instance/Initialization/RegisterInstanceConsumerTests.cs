using Core.OS.DbContext;
using Core.OS.Instance.Commands;
using Core.OS.Instance.Consumers;
using MassTransit;
using Microsoft.Extensions.DependencyInjection;
using Sdk;
using Sdk.Backend.Persistence;
using Sdk.Instance;
using Sdk.Testing.Backend;
using Xunit;

namespace Core.OS.Tests.Instance.Initialization;

public class RegisterInstanceConsumerTests : TestWithDbContextSqlite<ApplicationDbContextSqlite>
{
    private readonly Action<IBusRegistrationConfigurator> _configureServices;

    public RegisterInstanceConsumerTests()
        => _configureServices = services =>
        {
            services.AddConsumer<RegisterInstanceConsumer>();
            services.AddSingleton<IApplicationDbContext>(_ => TestDbContext);
            services.AddSingleton(new ModuleContextTypeInformation(Constants.SystemModuleId, typeof(ApplicationDbContext), typeof(ApplicationDbContext).AssemblyQualifiedName!));
            services.AddRoutingSlipBuilderFactory();
        };

    [Fact]
    public async Task Register_standalone_should_be_consumed()
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
    public async Task Register_new_slave_instance_should_trigger_synchronisation()
    {
        // Arrange
        await using var tester = new MassTransitTester(_configureServices);
        var command = new RegisterInstance
        {
            Name = "Test",
            InstanceId = Guid.NewGuid(),
            Type = InstanceType.Slave
        };

        command.InstalledModules.Add(Constants.SystemModuleId);

        // Act + Assert
        await tester.TestCommand<RegisterInstance, RegisterInstanceConsumer>(command);
    }

    [Fact]
    public async Task Register_slave_should_be_consumed()
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
