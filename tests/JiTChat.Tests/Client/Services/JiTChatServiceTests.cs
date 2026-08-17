using AutoFixture;
using JiTChat.Client.Contracts;
using JiTChat.Client.Services;
using JiTChat.Public.Commands;
using JiTChat.Public.Contracts;
using JiTChat.Public.Events;
using Sdk.Client.Infrastructure;
using Sdk.Testing.Client;

namespace JiTChat.Tests.Client.Services;

public sealed class JiTChatServiceTests
{
    private readonly Fixture _fixture = new();

    [Fact]
    public async Task Should_mark_all_messages_as_read()
    {
        // Arrange
        var clientMediatorMock = Substitute.For<IUiMediator>();
        clientMediatorMock.Register(Arg.Any<IJiTChatService>())
            .Returns(Substitute.For<IDisposable>());

        using var service = new JiTChatService(clientMediatorMock);

        var message = _fixture.Create<MessagePublished>();
        message.Message.Message = "Reeeeee";
        message.Message.User = "Me";

        var clientContext = ClientContextFactory.Create(message);

        await service.Consume(clientContext, TestContext.Current.CancellationToken);

        // Act
        service.MarkAllMessagesAsRead();

        // Assert
        Assert.Equal(0, service.UnreadMessages);
    }

    [Fact]
    public async Task Should_send_message_command()
    {
        // Arrange
        var clientMediatorMock = Substitute.For<IUiMediator>();
        clientMediatorMock.Register(Arg.Any<IJiTChatService>())
            .Returns(Substitute.For<IDisposable>());
        var called = false;

        using var service = new JiTChatService(clientMediatorMock);

        await clientMediatorMock.Send(Arg.Do<PublishMessage>(command =>
            {
                Assert.Equal("Me", command.Message.User);
                Assert.Equal("message text", command.Message.Message);
                called = true;
            }), Arg.Any<CancellationToken>());

        // Act
        await service.SendMessage("Me", "message text");

        // Assert
        Assert.True(called);
    }

    [Fact]
    public async Task Should_throw_message_published_event()
    {
        // Arrange
        var clientMediatorMock = Substitute.For<IUiMediator>();
        clientMediatorMock.Register(Arg.Any<IJiTChatService>())
            .Returns(Substitute.For<IDisposable>());

        using var service = new JiTChatService(clientMediatorMock);

        var firedMessages = new List<ChatMessage>();

        service.OnMessagePublished += (m) =>
        {
            firedMessages.Add(m);
        };

        var message = _fixture.Create<MessagePublished>();
        message.Message.Message = "Reeeeee";
        message.Message.User = "Me";

        var clientContext = ClientContextFactory.Create(message);

        // Act
        await service.Consume(clientContext, TestContext.Current.CancellationToken);

        // Assert
        Assert.Single(service.ChatMessages, m => Equals("Reeeeee", m.Message) && Equals("Me", m.User));
        Assert.Single(firedMessages, m => Equals("Reeeeee", m.Message) && Equals("Me", m.User));
        Assert.Equal(1, service.UnreadMessages);
    }

    [Fact]
    public async Task Should_throw_unread_messages_changed_event()
    {
        // Arrange
        var clientMediatorMock = Substitute.For<IUiMediator>();
        clientMediatorMock.Register(Arg.Any<IJiTChatService>())
            .Returns(Substitute.For<IDisposable>());

        using var service = new JiTChatService(clientMediatorMock);

        var firedMessages = 0;

        service.OnUnreadMessagesChanged += () => firedMessages++;

        var message = _fixture.Create<MessagePublished>();
        message.Message.Message = "Reeeeee";
        message.Message.User = "Me";

        var clientContext = ClientContextFactory.Create(message);

        // Act + Assert
        await service.Consume(clientContext, TestContext.Current.CancellationToken);

        Assert.Equal(1, firedMessages);

        service.MarkAllMessagesAsRead();

        Assert.Equal(2, firedMessages);
    }
}
