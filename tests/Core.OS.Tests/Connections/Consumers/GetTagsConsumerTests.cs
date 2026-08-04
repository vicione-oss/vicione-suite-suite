using Core.OS.Connections.Consumers;
using Core.OS.DbContext;
using MassTransit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NSubstitute.ExceptionExtensions;
using Sdk.Connections.Contracts;
using Sdk.Connections.Requests;
using Sdk.Testing.Backend;

namespace Core.OS.Tests.Connections.Consumers;

public sealed class GetTagsConsumerTests : TestWithDbContextSqlite<ConnectionDbContextSqlite>
{
    private readonly Action<IBusRegistrationConfigurator> _configureServices;

    public GetTagsConsumerTests()
        => _configureServices = cfg =>
        {
            cfg.AddConsumer<GetTagsConsumer>();
            cfg.AddSingleton<IConnectionDbContext>(_ => TestDbContext);
            cfg.AddSingleton(Substitute.For<ILogger<GetTagsConsumer>>());
        };

    [Fact]
    public async Task Should_return_error_response_on_exception()
    {
        // Arrange
        var db = Substitute.For<IConnectionDbContext>();
        Action<IBusRegistrationConfigurator> configureServices = cfg =>
        {
            cfg.AddConsumer<GetTagsConsumer>();
            cfg.AddSingleton(db);
            cfg.AddSingleton(Substitute.For<ILogger<GetTagsConsumer>>());
        };

        await using var tester = new MassTransitTester(configureServices);

        db.Tags.Throws(new ArgumentException("Test"));

        // Act
        var response = await tester.TestRequest<GetTagsResponse, GetTags>(new GetTags());

        // Assert
        response.Tags.Should().BeEmpty();
        response.RequestError.Should().NotBeNull();
        response.RequestError!.Message.Should().Be("Test");
        response.RequestError.ErrorCode.Should().Be(0);
    }

    [Fact]
    public async Task Should_return_all_tags()
    {
        // Arrange
        await using var tester = new MassTransitTester(_configureServices);
        var dbContext = tester.Services.GetRequiredService<IConnectionDbContext>();

        var firstTag = new Tag("first", Guid.NewGuid());
        var secondTag = new Tag("second", Guid.NewGuid());

        dbContext.Tags.Add(firstTag);
        dbContext.Tags.Add(secondTag);

        await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var response = await tester.TestRequest<GetTagsResponse, GetTags>(new());

        // Assert
        response.Should().NotBeNull();
        response.Tags.Count.Should().Be(2);
        response.Tags.Should().Contain(firstTag);
        response.Tags.Should().Contain(secondTag);
    }

    [Fact]
    public async Task Should_return_empty_list_if_no_tags_exist()
    {
        // Arrange
        await using var tester = new MassTransitTester(_configureServices);
        var dbContext = tester.Services.GetRequiredService<IConnectionDbContext>();

        // Act
        var response = await tester.TestRequest<GetTagsResponse, GetTags>(new GetTags());

        // Assert
        response.Tags.Should().BeEmpty();
    }
}
