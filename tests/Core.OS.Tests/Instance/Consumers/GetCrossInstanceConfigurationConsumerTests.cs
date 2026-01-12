using AwesomeAssertions;
using Core.OS.DbContext;
using Core.OS.Instance.Consumers;
using Core.Shared.Instance.Contracts;
using Core.Shared.Instance.Requests;
using MassTransit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Sdk.Testing.Backend;
using Xunit;

namespace Core.OS.Tests.Instance.Consumers;

public sealed class GetCrossInstanceConfigurationConsumerTests : TestWithDbContextSqlite<ApplicationDbContextSqlite>
{
    private readonly Action<IBusRegistrationConfigurator> _configureServices;

    public GetCrossInstanceConfigurationConsumerTests()
        => _configureServices = cfg =>
        {
            // consumer needs
            cfg.AddConsumer<GetCrossInstanceConfigurationConsumer>();
            cfg.AddSingleton(Substitute.For<ILogger<GetCrossInstanceConfigurationConsumer>>);
            cfg.AddSingleton<IApplicationDbContext>(_ => TestDbContext);
        };

    [Fact]
    public async Task Request_should_be_consumed_and_return_default_configuration()
    {
        // Arrange
        await using var tester = new MassTransitTester(_configureServices);
        var request = new GetCrossInstanceConfiguration();

        // Act
        var response = await tester.TestRequest<GetCrossInstanceConfigurationResponse, GetCrossInstanceConfiguration>(request);

        // Assert
        response.Should().NotBeNull();
        response.CrossInstanceConfiguration.Should().NotBeNull();
        response.RequestError.Should().BeNull();
    }

    [Fact]
    public async Task Request_should_return_existing_cross_configuration()
    {
        // Arrange
        await using var tester = new MassTransitTester(_configureServices);
        var request = new GetCrossInstanceConfiguration();
        var config = new CrossInstanceConfiguration() {  Id = Guid.NewGuid() };

        TestDbContext.CrossInstanceConfiguration.Add(config);
        await TestDbContext.SaveChangesAsync();

        // Act
        var response = await tester.TestRequest<GetCrossInstanceConfigurationResponse, GetCrossInstanceConfiguration>(request);

        // Assert
        response.Should().NotBeNull();
        response.CrossInstanceConfiguration.Should().BeEquivalentTo(config);
        response.RequestError.Should().BeNull();
    }
}
