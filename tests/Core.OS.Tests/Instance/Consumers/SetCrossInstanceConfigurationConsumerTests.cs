using AwesomeAssertions;
using Core.OS.DbContext;
using Core.OS.Instance.Consumers;
using Core.OS.Modules;
using Core.Shared.Instance.Commands;
using Core.Shared.Instance.Contracts;
using Core.Shared.Instance.Events;
using Core.UiHosting;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Sdk.Testing.Backend;
using TestUiHost;
using Xunit;

namespace Core.OS.Tests.Instance.Consumers;

public class SetCrossInstanceConfigurationConsumerTests : TestWithDbContextSqlite<ApplicationDbContextSqlite>
{
    private readonly Action<IBusRegistrationConfigurator> _configureServices;
    private readonly IModuleHost _moduleHost = Substitute.For<IModuleHost>();

    public SetCrossInstanceConfigurationConsumerTests()
        => _configureServices = services =>
        {
            services.AddConsumer<SetCrossInstanceConfigurationConsumer>();
            services.AddSingleton(_moduleHost);
            services.AddSingleton<IApplicationDbContext>(_ => TestDbContext);
            services.AddSingleton(Substitute.For<ILogger<SetCrossInstanceConfigurationConsumer>>());
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

        var added = await TestDbContext.CrossInstanceConfiguration.AsNoTracking().SingleAsync(TestContext.Current.CancellationToken);
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
        await TestDbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act + Assert
        await tester.TestCommand<SetCrossInstanceConfiguration, SetCrossInstanceConfigurationConsumer>(command);

        var updated = await TestDbContext.CrossInstanceConfiguration.AsNoTracking().SingleAsync(TestContext.Current.CancellationToken);
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
        await TestDbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act + Assert
        var changeEvent = await tester.TestCommand<SetCrossInstanceConfiguration, SetCrossInstanceConfigurationConsumer, CrossInstanceConfigurationChanged>(command);

        var updated = await TestDbContext.CrossInstanceConfiguration.AsNoTracking().SingleAsync(TestContext.Current.CancellationToken);
        changeEvent.CrossInstanceConfiguration.Id.Should().Be(config.Id);
        changeEvent.CrossInstanceConfiguration.CultureName.Should().Be("de-DE");
        changeEvent.CorrelationId.Should().Be(command.CorrelationId);

        changeEvent.CrossInstanceConfiguration.Should().BeEquivalentTo(updated);
    }

    [Fact]
    public async Task Should_update_uihost_default_culture()
    {
        //GetModules()

        // Arrange
        await using var tester = new MassTransitTester(_configureServices);
        var command = new SetCrossInstanceConfiguration("de-DE", TimeZoneInfo.Local.Id);
        var config = new CrossInstanceConfiguration() { Id = Guid.NewGuid(), CultureName = "en-GB" };
        var module = Substitute.For<IUiHostModule>();
        var host = new TestUiHostBackend();

        _moduleHost.GetModules().Returns([host]);

        TestDbContext.CrossInstanceConfiguration.Add(config);
        await TestDbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act + Assert
        await tester.TestCommand<SetCrossInstanceConfiguration, SetCrossInstanceConfigurationConsumer>(command);

        host.GetDefaultRequestCulture().Should().Be("de-DE");
    }
}
