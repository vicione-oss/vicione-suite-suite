using Core.OS.DbContext;
using Core.OS.Instance.Consumers;
using Core.Shared.Instance.Contracts;
using Core.Shared.Instance.Requests;
using MassTransit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Sdk.Testing.Backend;

namespace Core.OS.Tests.Instance.Consumers;

public sealed class GetCrossInstanceConfigurationConsumerTests : TestWithDbContextSqlite<ApplicationDbContextSqlite>
{
    private readonly Action<IBusRegistrationConfigurator> _configureServices;

    public GetCrossInstanceConfigurationConsumerTests()
        => _configureServices = cfg =>
        {
            // Required by the consumer.
            cfg.AddConsumer<GetCrossInstanceConfigurationConsumer>();
            cfg.AddSingleton(Substitute.For<ILogger<GetCrossInstanceConfigurationConsumer>>);
            cfg.AddSingleton<IApplicationDbContext>(_ => TestDbContext);
        };

    [Fact]
    public async Task Should_return_default_configuration_when_none_exists()
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
    public async Task Should_return_existing_cross_configuration()
    {
        // Arrange
        await using var tester = new MassTransitTester(_configureServices);
        var request = new GetCrossInstanceConfiguration();
        var config = new CrossInstanceConfiguration() {  Id = Guid.NewGuid() };

        TestDbContext.CrossInstanceConfiguration.Add(config);
        await TestDbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var response = await tester.TestRequest<GetCrossInstanceConfigurationResponse, GetCrossInstanceConfiguration>(request);

        // Assert
        response.Should().NotBeNull();
        response.CrossInstanceConfiguration.Should().BeEquivalentTo(config);
        response.RequestError.Should().BeNull();
    }
}
