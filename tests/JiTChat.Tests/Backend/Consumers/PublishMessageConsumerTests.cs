using AutoFixture;
using JiTChat.Backend.Consumers;
using JiTChat.Public.Commands;
using JiTChat.Public.Events;
using MassTransit;
using Microsoft.Extensions.DependencyInjection;
using Sdk.Testing.Backend;

namespace JiTChat.Tests.Backend.Consumers;

public class PublishMessageConsumerTests
{
    private readonly Action<IBusRegistrationConfigurator> _configureServices;
    private readonly Fixture _fixture = new();

    public PublishMessageConsumerTests()
    {
        _configureServices = cfg =>
        {
            // consumer needs
            cfg.AddConsumer<PublishMessageConsumer>();
            cfg.AddSingleton<IRoutingSlipBuilder>(new RoutingSlipBuilder(NewId.NextGuid()));
        };
    }

    [Fact]
    public async Task Command_should_be_consumed()
    {
        // Arrange
        await using var tester = new MassTransitTester(_configureServices);
        var command = new PublishMessage()
        {
            Message = new Public.Contracts.ChatMessage()
            {
                Message = "Eyy",
            }
        };

        // Act + Assert 
        await tester.TestCommand<PublishMessage, PublishMessageConsumer>(command);
    }

    [Fact]
    public async Task Publish_message_should_publish_changed_event()
    {
        // Arrange
        await using var tester = new MassTransitTester(_configureServices);
        var message = _fixture.Create<Public.Contracts.ChatMessage>();
        var command = new PublishMessage()
        {
            Message = message
        };

        // Act
        await tester.TestCommand<PublishMessage, PublishMessageConsumer>(command);

        // Assert
        Assert.True(await tester.Harness.Published.Any<MessagePublished>());
    }
}
