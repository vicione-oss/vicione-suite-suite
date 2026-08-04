using Blazor.Server.Backend.UserManagement;
using Core.Shared.UserManagement.Commands;
using Core.Shared.UserManagement.Contracts;
using Core.Shared.UserManagement.Events;
using MassTransit.Testing;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Sdk.Testing.Backend;

namespace Blazor.Server.Tests.UserManagement;

public sealed class DeleteExternalLoginConsumerTest
{
    private const string LoginProvider = "OpenIdConnect";
    private const string ProviderKey = "subject-1";

    private readonly UserManager<SuiteUser> _userManager = TestFactory.CreateUserManager();
    private readonly ILogger<DeleteExternalLoginConsumer> _logger = Substitute.For<ILogger<DeleteExternalLoginConsumer>>();

    [Fact]
    public async Task Should_return_an_error_when_user_is_unknown()
    {
        // Arrange
        var userId = "unknown-user-id";
        var request = new DeleteExternalLogin
        {
            UserId = userId,
            LoginProvider = LoginProvider,
            ProviderKey = ProviderKey
        };

        _userManager.FindByIdAsync(userId).Returns((SuiteUser?)null);

        await using var tester = CreateTester();

        // Act
        await tester.TestCommand<DeleteExternalLogin, DeleteExternalLoginConsumer>(request);

        // Assert
        var message = await tester.Harness.Published
            .SelectAsync<ExternalLoginDeletionCompleted>(TestContext.Current.CancellationToken).FirstOrDefault();
        message.Should().NotBeNull();
        message.Context.Message.ErrorInfo.Should().NotBeNull();
        message.Context.Message.ErrorInfo!.ErrorCode.Should().Be((int)ExternalLoginError.UserNotFound);
        await _userManager.DidNotReceive().RemoveLoginAsync(Arg.Any<SuiteUser>(), Arg.Any<string>(), Arg.Any<string>());
    }

    [Fact]
    public async Task Should_refuse_to_remove_the_only_credential()
    {
        // Arrange
        var user = new SuiteUser { Id = "user-1", UserName = "alice" };
        var request = new DeleteExternalLogin
        {
            UserId = user.Id,
            LoginProvider = LoginProvider,
            ProviderKey = ProviderKey
        };

        _userManager.FindByIdAsync(user.Id).Returns(user);
        _userManager.HasPasswordAsync(user).Returns(false);
        _userManager.GetPasskeysAsync(user).Returns(Task.FromResult<IList<UserPasskeyInfo>>([]));
        _userManager.GetLoginsAsync(user)
            .Returns(Task.FromResult<IList<UserLoginInfo>>(
                [new UserLoginInfo(LoginProvider, ProviderKey, null)]));

        await using var tester = CreateTester();

        // Act
        await tester.TestCommand<DeleteExternalLogin, DeleteExternalLoginConsumer>(request);

        // Assert
        var message = await tester.Harness.Published
            .SelectAsync<ExternalLoginDeletionCompleted>(TestContext.Current.CancellationToken).FirstOrDefault();
        message.Should().NotBeNull();
        message.Context.Message.ErrorInfo.Should().NotBeNull();
        message.Context.Message.ErrorInfo!.ErrorCode.Should().Be((int)ExternalLoginError.LastCredential);
        await _userManager.DidNotReceive().RemoveLoginAsync(Arg.Any<SuiteUser>(), Arg.Any<string>(), Arg.Any<string>());
    }

    [Fact]
    public async Task Should_remove_login_when_user_has_a_password()
    {
        // Arrange
        var user = new SuiteUser { Id = "user-1", UserName = "alice" };
        var request = new DeleteExternalLogin
        {
            UserId = user.Id,
            LoginProvider = LoginProvider,
            ProviderKey = ProviderKey
        };

        _userManager.FindByIdAsync(user.Id).Returns(user);
        _userManager.HasPasswordAsync(user).Returns(true);
        _userManager.RemoveLoginAsync(user, LoginProvider, ProviderKey).Returns(IdentityResult.Success);

        await using var tester = CreateTester();

        // Act
        await tester.TestCommand<DeleteExternalLogin, DeleteExternalLoginConsumer>(request);

        // Assert
        var message = await tester.Harness.Published
            .SelectAsync<ExternalLoginDeletionCompleted>(TestContext.Current.CancellationToken).FirstOrDefault();
        message.Should().NotBeNull();
        message.Context.Message.ErrorInfo.Should().BeNull();
        await _userManager.Received(1).RemoveLoginAsync(user, LoginProvider, ProviderKey);
    }

    [Fact]
    public async Task Should_remove_login_when_user_has_a_passkey()
    {
        // Arrange
        var user = new SuiteUser { Id = "user-1", UserName = "alice" };
        var request = new DeleteExternalLogin
        {
            UserId = user.Id,
            LoginProvider = LoginProvider,
            ProviderKey = ProviderKey
        };

        _userManager.FindByIdAsync(user.Id).Returns(user);
        _userManager.HasPasswordAsync(user).Returns(false);
        _userManager.GetPasskeysAsync(user).Returns(
            Task.FromResult<IList<UserPasskeyInfo>>(
                [new UserPasskeyInfo([],
                    [],
                    DateTimeOffset.UtcNow,
                    0,
                    null,
                    true,
                    true,
                    true,
                    [],
                    []) { Name = "I haz passkey" }]));
        _userManager.RemoveLoginAsync(user, LoginProvider, ProviderKey).Returns(IdentityResult.Success);

        await using var tester = CreateTester();

        // Act
        await tester.TestCommand<DeleteExternalLogin, DeleteExternalLoginConsumer>(request);

        // Assert
        var message = await tester.Harness.Published
            .SelectAsync<ExternalLoginDeletionCompleted>(TestContext.Current.CancellationToken).FirstOrDefault();
        message.Should().NotBeNull();
        message.Context.Message.ErrorInfo.Should().BeNull();
        await _userManager.Received(1).RemoveLoginAsync(user, LoginProvider, ProviderKey);
    }

    [Fact]
    public async Task Should_remove_login_when_another_login_exists()
    {
        // Arrange
        var user = new SuiteUser { Id = "user-1", UserName = "alice" };
        var request = new DeleteExternalLogin
        {
            UserId = user.Id,
            LoginProvider = LoginProvider,
            ProviderKey = ProviderKey
        };

        _userManager.FindByIdAsync(user.Id).Returns(user);
        _userManager.HasPasswordAsync(user).Returns(false);
        _userManager.GetPasskeysAsync(user).Returns(Task.FromResult<IList<UserPasskeyInfo>>([]));
        _userManager.GetLoginsAsync(user)
            .Returns(Task.FromResult<IList<UserLoginInfo>>(
            [
                new UserLoginInfo(LoginProvider, ProviderKey, null),
                new UserLoginInfo("OtherProvider", "other-key", null)
            ]));
        _userManager.RemoveLoginAsync(user, LoginProvider, ProviderKey).Returns(IdentityResult.Success);

        await using var tester = CreateTester();

        // Act
        await tester.TestCommand<DeleteExternalLogin, DeleteExternalLoginConsumer>(request);

        // Assert
        var message = await tester.Harness.Published
            .SelectAsync<ExternalLoginDeletionCompleted>(TestContext.Current.CancellationToken).FirstOrDefault();
        message.Should().NotBeNull();
        message.Context.Message.ErrorInfo.Should().BeNull();
        await _userManager.Received(1).RemoveLoginAsync(user, LoginProvider, ProviderKey);
    }

    [Fact]
    public async Task Should_return_an_error_when_identity_remove_fails()
    {
        // Arrange
        var user = new SuiteUser { Id = "user-1", UserName = "alice" };
        var request = new DeleteExternalLogin
        {
            UserId = user.Id,
            LoginProvider = LoginProvider,
            ProviderKey = ProviderKey
        };

        _userManager.FindByIdAsync(user.Id).Returns(user);
        _userManager.HasPasswordAsync(user).Returns(true);
        _userManager.RemoveLoginAsync(user, LoginProvider, ProviderKey)
            .Returns(IdentityResult.Failed(new IdentityError { Description = "boom" }));

        await using var tester = CreateTester();

        // Act
        await tester.TestCommand<DeleteExternalLogin, DeleteExternalLoginConsumer>(request);

        // Assert
        var message = await tester.Harness.Published
            .SelectAsync<ExternalLoginDeletionCompleted>(TestContext.Current.CancellationToken).FirstOrDefault();
        message.Should().NotBeNull();
        message.Context.Message.ErrorInfo.Should().NotBeNull();
        message.Context.Message.ErrorInfo!.ErrorCode.Should().Be((int)ExternalLoginError.Failed);
    }

    private MassTransitTester CreateTester() => new(cfg =>
    {
        cfg.AddConsumer<DeleteExternalLoginConsumer>();
        cfg.AddSingleton(_userManager);
        cfg.AddSingleton(_logger);
    });
}
