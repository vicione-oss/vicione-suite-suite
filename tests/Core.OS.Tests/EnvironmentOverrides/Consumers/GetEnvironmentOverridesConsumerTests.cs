using Core.OS.EnvironmentOverrides;
using Core.OS.EnvironmentOverrides.Consumers;
using Core.Shared.EnvironmentOverrides.Requests;
using MassTransit;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute.ExceptionExtensions;
using Sdk.Testing.Backend;

namespace Core.OS.Tests.EnvironmentOverrides.Consumers;

public sealed class GetEnvironmentOverridesConsumerTests
{
    private readonly IEnvironmentOverridesRepository _repository = Substitute.For<IEnvironmentOverridesRepository>();
    private readonly Action<IBusRegistrationConfigurator> _configureServices;

    public GetEnvironmentOverridesConsumerTests()
        => _configureServices = cfg =>
        {
            cfg.AddConsumer<GetEnvironmentOverridesConsumer>();
            cfg.AddSingleton(_repository);
        };

    [Fact]
    public async Task Should_return_overrides_from_repository()
    {
        // Arrange
        var overrides = new Dictionary<string, string>
        {
            ["OTEL_EXPORTER_OTLP_ENDPOINT"] = "http://collector:4317",
        };

        _repository.Get(Arg.Any<CancellationToken>()).Returns(overrides);

        await using var tester = new MassTransitTester(_configureServices);
        var request = new GetEnvironmentOverrides();

        // Act
        var response = await tester.TestInstanceDependentRequest<GetEnvironmentOverridesResponse, GetEnvironmentOverrides>(request);

        // Assert
        response.Should().NotBeNull();
        response.Overrides.Should().Equal(overrides);
        response.RequestError.Should().BeNull();
    }

    [Fact]
    public async Task Should_return_empty_response_when_no_overrides_stored()
    {
        // Arrange
        _repository.Get(Arg.Any<CancellationToken>()).Returns(new Dictionary<string, string>());

        await using var tester = new MassTransitTester(_configureServices);
        var request = new GetEnvironmentOverrides();

        // Act
        var response = await tester.TestInstanceDependentRequest<GetEnvironmentOverridesResponse, GetEnvironmentOverrides>(request);

        // Assert
        response.Overrides.Should().BeEmpty();
    }

    [Fact]
    public async Task Should_return_error_response_on_failure()
    {
        // Arrange
        _repository.Get(Arg.Any<CancellationToken>()).ThrowsAsync(new InvalidOperationException("test failure"));

        await using var tester = new MassTransitTester(_configureServices);
        var request = new GetEnvironmentOverrides();

        // Act
        var response = await tester.TestInstanceDependentRequest<GetEnvironmentOverridesResponse, GetEnvironmentOverrides>(request);

        // Assert
        response.Overrides.Should().BeEmpty();
        response.RequestError.Should().NotBeNull();
        response.RequestError!.Message.Should().Be("test failure");
    }
}
