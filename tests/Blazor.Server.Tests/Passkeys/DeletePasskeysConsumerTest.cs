using Blazor.Server.Backend.Passkeys;
using Core.Shared.Passkeys;
using Core.Shared.Passkeys.Commands;
using Core.Shared.UserManagement.Contracts;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Sdk.Testing.Backend;
using Xunit;
using AwesomeAssertions;
using Core.Shared.Passkeys.Events;
using MassTransit.Testing;

namespace Blazor.Server.Tests.Passkeys;

public class DeletePasskeysConsumerTest
{
    private readonly UserManager<SuiteUser> _userManagerMock = Substitute.For<UserManager<SuiteUser>>(
        Substitute.For<IUserStore<SuiteUser>>(),
        null,
        null,
        null,
        null,
        null,
        null,
        null,
        null);

    private readonly ILogger<DeletePasskeysConsumer> _loggerMock = Substitute.For<ILogger<DeletePasskeysConsumer>>();

    [Fact]
    public async Task Should_return_an_error_when_user_is_unknown()
    {
        // Arrange
        var userId = "unknown-user-id";
        var request = new DeletePasskeys
        {
            UserId = userId,
            PasskeyIds = ["passkey-1"]
        };

        _userManagerMock.FindByIdAsync(userId).Returns((SuiteUser?)null);

        await using var tester = new MassTransitTester(cfg =>
        {
            cfg.AddConsumer<DeletePasskeysConsumer>();
            cfg.AddSingleton(_userManagerMock);
            cfg.AddSingleton(_loggerMock);
        });

        // Act
        await tester.TestCommand<DeletePasskeys, DeletePasskeysConsumer>(request);

        // Assert
        var message = await tester.Harness.Published
            .SelectAsync<PasskeyDeletionCompleted>(TestContext.Current.CancellationToken).FirstOrDefault();
        message.Should().NotBeNull();
        message.Context.Message.ErrorInfo.Should().NotBeNull();
        message.Context.Message.ErrorInfo!.ErrorCode.Should().Be(404);
        message.Context.Message.ErrorInfo!.Message.Should().Contain("User not found");
    }

    [Fact]
    public async Task Should_delete_passkeys_successfully()
    {
        // Arrange
        var userId = "test-user-id";
        var user = new SuiteUser
        {
            Id = userId,
            UserName = "testuser"
        };
        var passkeyId1 = PasskeyIdConverter.EncodePasskeyId([1, 2, 3]);
        var passkeyId2 = PasskeyIdConverter.EncodePasskeyId([4, 5, 6]);

        var request = new DeletePasskeys
        {
            UserId = userId,
            PasskeyIds = [passkeyId1, passkeyId2]
        };

        _userManagerMock.FindByIdAsync(userId).Returns(user);
        _userManagerMock.RemovePasskeyAsync(user, Arg.Any<byte[]>()).Returns(Task.FromResult(new IdentityResult()));

        await using var tester = new MassTransitTester(cfg =>
        {
            cfg.AddConsumer<DeletePasskeysConsumer>();
            cfg.AddSingleton(_userManagerMock);
            cfg.AddSingleton(_loggerMock);
        });

        // Act
        await tester.TestCommand<DeletePasskeys, DeletePasskeysConsumer>(request);

        // Assert
        var message = await tester.Harness.Published
            .SelectAsync<PasskeyDeletionCompleted>(TestContext.Current.CancellationToken).FirstOrDefault();
        message.Should().NotBeNull();
        await _userManagerMock.Received(2).RemovePasskeyAsync(Arg.Is(user), Arg.Any<byte[]>());
    }

    [Fact]
    public async Task Should_handle_empty_passkey_ids_list()
    {
        // Arrange
        var userId = "test-user-id";
        var user = new SuiteUser
        {
            Id = userId,
            UserName = "testuser"
        };

        var request = new DeletePasskeys
        {
            UserId = userId,
            PasskeyIds = Array.Empty<string>()
        };

        _userManagerMock.FindByIdAsync(userId).Returns(user);

        await using var tester = new MassTransitTester(cfg =>
        {
            cfg.AddConsumer<DeletePasskeysConsumer>();
            cfg.AddSingleton(_userManagerMock);
            cfg.AddSingleton(_loggerMock);
        });

        // Act
        await tester.TestCommand<DeletePasskeys, DeletePasskeysConsumer>(request);

        // Assert
        var message = await tester.Harness.Published
            .SelectAsync<PasskeyDeletionCompleted>(TestContext.Current.CancellationToken).FirstOrDefault();
        message.Should().NotBeNull();
        await _userManagerMock.DidNotReceive().RemovePasskeyAsync(Arg.Any<SuiteUser>(), Arg.Any<byte[]>());
    }
}
