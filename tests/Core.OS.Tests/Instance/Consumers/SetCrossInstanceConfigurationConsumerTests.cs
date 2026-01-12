using AwesomeAssertions;
using Core.OS.DbContext;
using Core.OS.Instance.Consumers;
using Core.Shared.Instance.Commands;
using Core.Shared.Instance.Contracts;
using Core.Shared.Instance.Events;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Sdk.Testing.Backend;
using Xunit;

namespace Core.OS.Tests.Instance.Consumers;

public class SetCrossInstanceConfigurationConsumerTests : TestWithDbContextSqlite<ApplicationDbContextSqlite>
{
    private readonly Action<IBusRegistrationConfigurator> _configureServices;

    public SetCrossInstanceConfigurationConsumerTests()
        => _configureServices = services =>
        {
            services.AddConsumer<SetCrossInstanceConfigurationConsumer>();
            services.AddSingleton<IApplicationDbContext>(_ => TestDbContext);
            services.AddSingleton<ILogger<SetCrossInstanceConfigurationConsumer>>(Substitute.For<ILogger<SetCrossInstanceConfigurationConsumer>>());
        };

    [Fact]
    public async Task Should_consume_command()
    {
        // Arrange
        await using var tester = new MassTransitTester(_configureServices);
        var command = new SetCrossInstanceConfiguration("de-DE", TimeZoneInfo.Local.Id);

        // Act + Assert
        await tester.TestCommand<SetCrossInstanceConfiguration, SetCrossInstanceConfigurationConsumer>(command);
    }

    [Fact]
    public async Task Should_add_cross_configuration_if_none_exists()
    {
        // Arrange
        await using var tester = new MassTransitTester(_configureServices);
        var command = new SetCrossInstanceConfiguration("de-DE", TimeZoneInfo.Local.Id);

        // Act + Assert
        await tester.TestCommand<SetCrossInstanceConfiguration, SetCrossInstanceConfigurationConsumer>(command);

        var added = await TestDbContext.CrossInstanceConfiguration.AsNoTracking().SingleAsync();
        added.CultureName.Should().Be("de-DE");
        added.TimeZoneId.Should().Be(TimeZoneInfo.Local.Id);
    }

    [Fact]
    public async Task Should_update_existing_cross_configuration()
    {
        // Arrange
        await using var tester = new MassTransitTester(_configureServices);
        var command = new SetCrossInstanceConfiguration("de-DE", TimeZoneInfo.Local.Id);
        var config = new CrossInstanceConfiguration() { Id = Guid.NewGuid(), CultureName = "en-GB" };

        TestDbContext.CrossInstanceConfiguration.Add(config);
        await TestDbContext.SaveChangesAsync();

        // Act + Assert
        await tester.TestCommand<SetCrossInstanceConfiguration, SetCrossInstanceConfigurationConsumer>(command);

        var updated = await TestDbContext.CrossInstanceConfiguration.AsNoTracking().SingleAsync();
        updated.Id.Should().Be(config.Id);
        updated.CultureName.Should().Be("de-DE");
    }

    [Fact]
    public async Task Should_send_correlating_change_event_on_success()
    {
        // Arrange
        await using var tester = new MassTransitTester(_configureServices);
        var command = new SetCrossInstanceConfiguration("de-DE", TimeZoneInfo.Local.Id)
        {
            CorrelationId = Guid.NewGuid()
        };
        var config = new CrossInstanceConfiguration() { Id = Guid.NewGuid(), CultureName = "en-GB" };

        TestDbContext.CrossInstanceConfiguration.Add(config);
        await TestDbContext.SaveChangesAsync();

        // Act + Assert
        var changeEvent = await tester.TestCommand<SetCrossInstanceConfiguration, SetCrossInstanceConfigurationConsumer, CrossInstanceConfigurationChanged>(command);

        var updated = await TestDbContext.CrossInstanceConfiguration.AsNoTracking().SingleAsync();
        changeEvent.CrossInstanceConfiguration.Id.Should().Be(config.Id);
        changeEvent.CrossInstanceConfiguration.CultureName.Should().Be("de-DE");
        changeEvent.CorrelationId.Should().Be(command.CorrelationId);
    }
}
