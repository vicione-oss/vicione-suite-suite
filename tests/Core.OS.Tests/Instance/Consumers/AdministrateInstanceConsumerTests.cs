using Core.OS.DbContext;
using Core.OS.Instance;
using Core.OS.Instance.Consumers;
using Core.Shared.Instance.Commands;
using Core.Shared.Instance.Contracts;
using MassTransit;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Sdk;
using Sdk.Backend.Persistence;
using Sdk.Instance;
using Sdk.Testing.Backend;
using Xunit;

namespace Core.OS.Tests.Instance.Consumers;

public class AdministrateInstanceConsumerTests : TestWithDbContextSqlite<ApplicationDbContextSqlite>
{
    private readonly Action<IBusRegistrationConfigurator> _configureServices;
    private readonly ILocalInstanceInformationProvider _localInstanceProviderMock = Substitute.For<ILocalInstanceInformationProvider>();

    private readonly InstanceInformation _masterInstance = new()
    {
        Id = Guid.NewGuid(),
        Type = InstanceType.Master,
        InstalledModules = [Constants.SystemModuleId]
    };

    public AdministrateInstanceConsumerTests()
    {
        _localInstanceProviderMock.Local
            .Returns(_masterInstance);

        _configureServices = cfg =>
        {
            cfg.AddConsumer<AdministrateInstanceConsumer>();
            cfg.AddSingleton(_localInstanceProviderMock);
            cfg.AddSingleton<IApplicationDbContext>(_ => TestDbContext);
            cfg.AddSingleton(new ModuleContextTypeInformation(Constants.SystemModuleId, typeof(ApplicationDbContext), typeof(ApplicationDbContext).AssemblyQualifiedName!));
            cfg.AddRoutingSlipBuilderFactory();
        };
    }

    [Fact]
    public async Task Administer_command_should_be_consumed()
    {
        // Arrange
        await using var tester = new MassTransitTester(_configureServices);
        var command = new AdministrateInstance
        {
            Action = AdministrateInstanceAction.Delete,
            InstanceId = Guid.NewGuid()
        };

        // Act + Assert
        await tester.TestCommand<AdministrateInstance, AdministrateInstanceConsumer>(command);
    }
}
