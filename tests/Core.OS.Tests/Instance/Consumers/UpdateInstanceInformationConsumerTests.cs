using Core.OS.DbContext;
using Core.OS.Instance;
using Core.OS.Instance.Consumers;
using Core.OS.Instance.Services;
using Core.OS.Tests.Extensions;
using Core.Shared.Instance.Commands;
using Core.Shared.Instance.Contracts;
using Core.Shared.Instance.Events;
using AwesomeAssertions;
using MassTransit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Sdk.Instance;
using Sdk.Testing.Backend;
using Xunit;

namespace Core.OS.Tests.Instance.Consumers;

public class UpdateInstanceInformationConsumerTests : TestWithDbContextSqlite<ApplicationDbContextSqlite>
{
    private readonly Action<IBusRegistrationConfigurator> _configureServices;
    private readonly InMemoryClusterInformationProvider _informationProvider = new(Substitute.For<ILogger<InMemoryClusterInformationProvider>>());
    private readonly ILocalInstanceInformationProvider _localInformationProvider = Substitute.For<ILocalInstanceInformationProvider>();

    public UpdateInstanceInformationConsumerTests()
    {
        _configureServices = cfg =>
        {
            cfg.AddConsumer<UpdateInstanceInformationConsumer>();
            cfg.AddSingleton<IApplicationDbContext>(_ => TestDbContext);
            cfg.AddSingleton(_informationProvider);
            cfg.AddSingleton(_localInformationProvider);
        };
    }

    [Fact]
    public async Task Should_update_existing_instance_information()
    {
        // Arrange
        await using var tester = new MassTransitTester(_configureServices);
        var instance = tester.Services.GetRequiredService<IApplicationDbContext>()
            .SeedInstanceInfos(1)
            .First();

        await _informationProvider.AddNewInstance(instance);

        var update = new InstanceInformation
        {
            Id = instance.Id,
            Name = "ChangeName",
            Description = "ChangedDescripion",
            FormattedName = "ChangedFormattedName",
            SerialNumber = instance.SerialNumber,
            SdkVersion = instance.SdkVersion,
        };

        var command = new UpdateInstanceInformation(update);

        // Act
        var triggeredEvent = await tester.TestCommand<UpdateInstanceInformation, UpdateInstanceInformationConsumer, InstanceInformationUpdated>(command);

        // Assert
        Assert.NotNull(triggeredEvent);
        Assert.IsType<InstanceInformationUpdated>(triggeredEvent);

        triggeredEvent.Success.Should().BeTrue();
        triggeredEvent.InstanceInformation.Id.Should().Be(instance.Id);
        triggeredEvent.InstanceInformation.Name.Should().Be(update.Name);
        triggeredEvent.InstanceInformation.FormattedName.Should().Be(update.FormattedName);
        triggeredEvent.InstanceInformation.Description.Should().Be(update.Description);

        var inMemoryInstances = await _informationProvider.GetInstancesInCluster(TestContext.Current.CancellationToken);
        inMemoryInstances.Should().ContainSingle(i => i.Id == instance.Id && i.Name == update.Name && i.FormattedName == update.FormattedName);

        _localInformationProvider.Received().UpdateLocal(Arg.Is<IInstanceInformation>(i => i.Id == instance.Id && i.Name == update.Name && i.FormattedName == update.FormattedName));
    }

    [Fact]
    public async Task Should_not_success_on_not_existing_instance_information()
    {
        // Arrange
        await using var tester = new MassTransitTester(_configureServices);

        var update = new InstanceInformation
        {
            Id = Guid.NewGuid(),
            Name = "ChangeName",
            Description = "ChangedDescripion",
            FormattedName = "ChangedFormattedName",
        };

        var command = new UpdateInstanceInformation(update);

        // Act
        var triggeredEvent = await tester.TestCommand<UpdateInstanceInformation, UpdateInstanceInformationConsumer, InstanceInformationUpdated>(command);

        // Assert
        Assert.NotNull(triggeredEvent);
        Assert.IsType<InstanceInformationUpdated>(triggeredEvent);

        triggeredEvent.Success.Should().BeFalse();
    }
}
