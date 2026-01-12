using Core.OS.DbContext;
using Core.OS.Instance.Consumers;
using Core.OS.Tests.Extensions;
using Core.Shared.Instance.Requests;
using AwesomeAssertions;
using MassTransit;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Sdk.Testing.Backend;
using Xunit;

namespace Core.OS.Tests.Instance.Consumers;

public class GetInstancesConsumerTests : TestWithDbContextSqlite<ApplicationDbContextSqlite>
{
    private Action<IBusRegistrationConfigurator> _configureServices;

    public GetInstancesConsumerTests()
        => _configureServices = cfg =>
        {
            // consumer needs
            cfg.AddConsumer<GetInstancesConsumer>();
            cfg.AddSingleton<IApplicationDbContext>(_ => TestDbContext);
        };

    [Fact]
    public async Task Request_should_be_consumed()
    {
        // Arrange
        await using var tester = new MassTransitTester(_configureServices);
        var request = new GetInstances();

        // Act
        var response = await tester.TestRequest<GetInstancesResponse, GetInstances>(request);

        // Assert
        response.Should().NotBeNull();
        response.Instances.Should().BeEmpty();
    }

    [Fact]
    public async Task Request_should_return_instances_information()
    {
        // Arrange
        await using var tester = new MassTransitTester(_configureServices);
        var instances = tester.Services.GetRequiredService<IApplicationDbContext>().SeedInstanceInfos(5).ToList();
        var request = new GetInstances();

        // Act
        var response = await tester.TestRequest<GetInstancesResponse, GetInstances>(request);

        // Assert
        response.Should().NotBeNull();
        response.Instances.Should().BeEquivalentTo(instances);
    }

    [Fact]
    public async Task Request_with_id_should_return_instance_information()
    {
        // Arrange
        await using var tester = new MassTransitTester(_configureServices);
        var firstInstanceId = tester.Services.GetRequiredService<IApplicationDbContext>().SeedInstanceInfos(5).First().Id;
        var request = new GetInstances(firstInstanceId);

        // Act
        var response = await tester.TestRequest<GetInstancesResponse, GetInstances>(request);

        // Assert
        response.Should().NotBeNull();
        response.Instances.Should().ContainSingle();
        response.Instances.Should().Contain(i => i.Id == firstInstanceId);
    }

    [Fact]
    public async Task Consume_should_publish_response_on_failure()
    {
        // Arrange
        var dbContextMock = Substitute.For<IApplicationDbContext>();
        dbContextMock.InstanceInfo.Throws(new ArgumentException("test"));

        _configureServices += cfg => cfg.AddSingleton(dbContextMock);

        await using var tester = new MassTransitTester(_configureServices);
        var request = new GetInstances(new());

        // Act
        var response = await tester.TestRequest<GetInstancesResponse, GetInstances>(request);

        // Assert 
        response.Should().BeEquivalentTo(new GetInstancesResponse([], new(0, "test")));
    }
}
