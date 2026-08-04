using Core.OS.DbContext;
using Core.OS.Instance.Consumers;
using Core.OS.Tests.Extensions;
using Core.Shared.Instance.Requests;
using MassTransit;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute.ExceptionExtensions;
using Sdk.Testing.Backend;

namespace Core.OS.Tests.Instance.Consumers;

public sealed class GetInstancesConsumerTests : TestWithDbContextSqlite<ApplicationDbContextSqlite>
{
    private readonly Action<IBusRegistrationConfigurator> _configureServices;

    public GetInstancesConsumerTests()
        => _configureServices = cfg =>
        {
            // consumer needs
            cfg.AddConsumer<GetInstancesConsumer>();
            cfg.AddSingleton<IApplicationDbContext>(_ => TestDbContext);
        };

    [Fact]
    public async Task Should_return_empty_list_when_no_instances()
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
    public async Task Should_return_instances_information()
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
    public async Task Should_return_matching_instance_when_filtered_by_id()
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
    public async Task Should_return_error_response_on_failure()
    {
        // Arrange
        var dbContextMock = Substitute.For<IApplicationDbContext>();
        dbContextMock.InstanceInfo.Throws(new ArgumentException("test"));

        Action<IBusRegistrationConfigurator> configureServices = _configureServices + (cfg => cfg.AddSingleton(dbContextMock));

        await using var tester = new MassTransitTester(configureServices);
        var request = new GetInstances(new());

        // Act
        var response = await tester.TestRequest<GetInstancesResponse, GetInstances>(request);

        // Assert 
        response.Should().BeEquivalentTo(new GetInstancesResponse([], new(0, "test")));
    }
}
