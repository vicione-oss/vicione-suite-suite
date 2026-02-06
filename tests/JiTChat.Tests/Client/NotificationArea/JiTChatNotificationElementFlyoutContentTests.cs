using System.Security.Claims;
using Blazor.Tests.Tools;
using Bunit;
using JiTChat.Client;
using JiTChat.Client.Contracts;
using JiTChat.Client.NotificationArea;
using JiTChat.Public.Contracts;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Sdk.Authorization;
using Sdk.Modules;

namespace JiTChat.Tests.Client.NotificationArea;

public sealed class JiTChatNotificationElementFlyoutContentTests
{
    private static BunitContext SetupTestContext(IJiTChatService? jitChatService = null)
    {
        var ctx = new BunitContext();

        var timeProvider = Substitute.For<TimeProvider>();
        timeProvider.LocalTimeZone.Returns(TimeZoneInfo.Utc);

        ctx.SetupSuiteServicesWithBlazorDx(setup =>
            setup.Services.AddKeyedScoped(Sdk.Constants.ClientTimeProviderServiceKey, (_, __) => timeProvider));

        if (jitChatService is null)
        {
            jitChatService = Substitute.For<IJiTChatService>();

            jitChatService.ChatMessages
                .Returns([]);
        }

        ctx.Services.AddSingleton(jitChatService);

        return ctx;
    }

    [Fact]
    public void Should_render_component()
    {
        using var ctx = SetupTestContext();

        var authContext = ctx.AddAuthorization();
        authContext.SetAuthorized("Eddy");

        // Act
        var sut = ctx.Render<JiTChatNotificationElementFlyoutContent>();

        // Assert
        Assert.NotNull(sut);
    }

    [Fact]
    public void Should_send_message()
    {
        var jitChatServiceMock = Substitute.For<IJiTChatService>();
        jitChatServiceMock.ChatMessages
            .Returns([]);

        jitChatServiceMock.SendMessage(Arg.Is("Eddy"), Arg.Is(string.Empty));

        using var ctx = SetupTestContext(jitChatServiceMock);

        var authContext = ctx.AddAuthorization();
        authContext
            .SetAuthorized("Eddy")
            .SetPolicies(ModulePolicyProvider.GetPolicy<JiTChatClientModule>())
            .SetClaims(new Claim(ModuleIdResolver.ResolveId<JiTChatClientModule>(), nameof(AccessLevel.Partial)));

        // Act
        var cut = ctx.Render<JiTChatNotificationElementFlyoutContent>();
        var button = cut.Find(".jit-chat-input > .btn");
        button.Click();

        // Assert
        jitChatServiceMock.Received(1).SendMessage("Eddy", string.Empty);
    }

    [Fact]
    public void Should_display_message()
    {
        var jitChatServiceMock = Substitute.For<IJiTChatService>();
        var messages = new List<ChatMessage>();
        jitChatServiceMock.ChatMessages
            .Returns(messages);

        using var ctx = SetupTestContext(jitChatServiceMock);

        var authContext = ctx.AddAuthorization();
        authContext
            .SetAuthorized("Eddy")
            .SetPolicies(ModulePolicyProvider.GetPolicy<JiTChatClientModule>())
            .SetClaims(new Claim(ModuleIdResolver.ResolveId<JiTChatClientModule>(), nameof(AccessLevel.Partial)));

        // Act
        var cut = ctx.Render<JiTChatNotificationElementFlyoutContent>();

        var message = new ChatMessage { User = "Bob", Message = "Huhu" };
        messages.Add(message);

        jitChatServiceMock.OnMessagePublished += Raise.Event<Action<ChatMessage>>(message);

        // Assert
        var messageInfos = cut.FindAll(".jit-chat-message-info > p");
        Assert.Single(messageInfos, e => Equals("Bob", e.InnerHtml));

        var messageBodies = cut.FindAll(".jit-chat-message > p");
        Assert.Single(messageBodies, e => Equals("Huhu", e.InnerHtml));
    }
}
