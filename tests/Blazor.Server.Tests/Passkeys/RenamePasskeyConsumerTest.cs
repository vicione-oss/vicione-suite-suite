using AwesomeAssertions;
using Blazor.Server.Backend.Passkeys;
using Core.Shared.Passkeys;
using Core.Shared.Passkeys.Commands;
using Core.Shared.Passkeys.Events;
using Core.Shared.UserManagement.Contracts;
using MassTransit.Testing;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Sdk.Testing.Backend;
using Xunit;

namespace Blazor.Server.Tests.Passkeys;

public sealed class RenamePasskeyConsumerTest
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

    private readonly ILogger<RenamePasskeyConsumer> _loggerMock =
        Substitute.For<ILogger<RenamePasskeyConsumer>>();

    [Fact]
    public async Task Should_return_an_error_when_user_is_unknown()
    {
        // Arrange
        const string userId = "unknown-user-id";
        var request = new RenamePasskey
        {
            UserId = userId,
            PasskeyId = PasskeyIdConverter.EncodePasskeyId([1, 2, 3]),
            NewName = "new name"
        };

        _userManagerMock.FindByIdAsync(userId).Returns((SuiteUser?)null);

        await using var tester = new MassTransitTester(cfg =>
        {
            cfg.AddConsumer<RenamePasskeyConsumer>();
            cfg.AddSingleton(_userManagerMock);
            cfg.AddSingleton(_loggerMock);
        });

        // Act
        await tester.TestCommand<RenamePasskey, RenamePasskeyConsumer>(request);

        // Assert
        var message = await tester.Harness.Published
            .SelectAsync<PasskeyRenamingCompleted>(TestContext.Current.CancellationToken)
            .FirstOrDefault();
        message.Should().NotBeNull();
        message.Context.Message.ErrorInfo.Should().NotBeNull();
        message.Context.Message.ErrorInfo!.ErrorCode.Should().Be((int)PasskeyError.UserNotFound);
    }

    [Fact]
    public async Task Should_return_an_error_when_passkey_is_unknown()
    {
        // Arrange
        const string userId = "test-user-id";
        var credentialId = new byte[] { 1, 2, 3 };
        var user = new SuiteUser
        {
            Id = userId,
            UserName = "testuser"
        };
        var request = new RenamePasskey
        {
            UserId = userId,
            PasskeyId = PasskeyIdConverter.EncodePasskeyId(credentialId),
            NewName = "new name"
        };

        _userManagerMock.FindByIdAsync(userId).Returns(user);
        _userManagerMock.GetPasskeysAsync(user).Returns(new List<UserPasskeyInfo>());

        await using var tester = new MassTransitTester(cfg =>
        {
            cfg.AddConsumer<RenamePasskeyConsumer>();
            cfg.AddSingleton(_userManagerMock);
            cfg.AddSingleton(_loggerMock);
        });

        // Act
        await tester.TestCommand<RenamePasskey, RenamePasskeyConsumer>(request);

        // Assert
        var message = await tester.Harness.Published
            .SelectAsync<PasskeyRenamingCompleted>(TestContext.Current.CancellationToken)
            .FirstOrDefault();
        message.Should().NotBeNull();
        message.Context.Message.ErrorInfo.Should().NotBeNull();
        message.Context.Message.ErrorInfo!.ErrorCode.Should().Be((int)PasskeyError.PasskeyNotFound);
    }

    [Fact]
    public async Task Should_rename_passkey_successfully()
    {
        // Arrange
        const string userId = "test-user-id";
        var credentialId = new byte[] { 1, 2, 3 };
        var user = new SuiteUser
        {
            Id = userId,
            UserName = "testuser"
        };
        var passkey = new UserPasskeyInfo(credentialId,
            [],
            DateTimeOffset.UtcNow,
            0,
            null,
            true,
            true,
            true,
            [],
            []) { Name = "old name" };
        var request = new RenamePasskey
        {
            UserId = userId,
            PasskeyId = PasskeyIdConverter.EncodePasskeyId(credentialId),
            NewName = "new name"
        };

        _userManagerMock.FindByIdAsync(userId).Returns(user);
        _userManagerMock.GetPasskeysAsync(user).Returns(new List<UserPasskeyInfo> { passkey });
        _userManagerMock.AddOrUpdatePasskeyAsync(user, Arg.Any<UserPasskeyInfo>()).Returns(IdentityResult.Success);

        await using var tester = new MassTransitTester(cfg =>
        {
            cfg.AddConsumer<RenamePasskeyConsumer>();
            cfg.AddSingleton(_userManagerMock);
            cfg.AddSingleton(_loggerMock);
        });

        // Act
        await tester.TestCommand<RenamePasskey, RenamePasskeyConsumer>(request);

        // Assert
        var message = await tester.Harness.Published
            .SelectAsync<PasskeyRenamingCompleted>(TestContext.Current.CancellationToken)
            .FirstOrDefault();
        message.Should().NotBeNull();
        message.Context.Message.ErrorInfo.Should().BeNull();
        await _userManagerMock.Received(1)
            .AddOrUpdatePasskeyAsync(user,
                Arg.Is<UserPasskeyInfo>(p => p.Name == "new name"));
    }

    [Fact]
    public async Task Should_skip_update_when_name_is_unchanged()
    {
        // Arrange
        const string userId = "test-user-id";
        const string existingName = "my passkey";
        var credentialId = new byte[] { 1, 2, 3 };
        var user = new SuiteUser
        {
            Id = userId,
            UserName = "testuser"
        };
        var passkey = new UserPasskeyInfo(credentialId,
            [],
            DateTimeOffset.UtcNow,
            0,
            null,
            true,
            true,
            true,
            [],
            []) { Name = existingName };
        var request = new RenamePasskey
        {
            UserId = userId,
            PasskeyId = PasskeyIdConverter.EncodePasskeyId(credentialId),
            NewName = existingName
        };

        _userManagerMock.FindByIdAsync(userId).Returns(user);
        _userManagerMock.GetPasskeysAsync(user).Returns(new List<UserPasskeyInfo> { passkey });

        await using var tester = new MassTransitTester(cfg =>
        {
            cfg.AddConsumer<RenamePasskeyConsumer>();
            cfg.AddSingleton(_userManagerMock);
            cfg.AddSingleton(_loggerMock);
        });

        // Act
        await tester.TestCommand<RenamePasskey, RenamePasskeyConsumer>(request);

        // Assert
        var message = await tester.Harness.Published
            .SelectAsync<PasskeyRenamingCompleted>(TestContext.Current.CancellationToken)
            .FirstOrDefault();
        message.Should().NotBeNull();
        message.Context.Message.ErrorInfo.Should().BeNull();
        await _userManagerMock.DidNotReceive()
            .AddOrUpdatePasskeyAsync(Arg.Any<SuiteUser>(), Arg.Any<UserPasskeyInfo>());
    }

    [Fact]
    public async Task Should_rename_passkey_when_only_casing_differs()
    {
        // Arrange
        const string userId = "test-user-id";
        const string existingName = "my passkey";
        var credentialId = new byte[] { 1, 2, 3 };
        var user = new SuiteUser
        {
            Id = userId,
            UserName = "testuser"
        };
        var passkey = new UserPasskeyInfo(credentialId,
            [],
            DateTimeOffset.UtcNow,
            0,
            null,
            true,
            true,
            true,
            [],
            []) { Name = existingName };
        var request = new RenamePasskey
        {
            UserId = userId,
            PasskeyId = PasskeyIdConverter.EncodePasskeyId(credentialId),
            NewName = existingName.ToUpperInvariant()
        };

        _userManagerMock.FindByIdAsync(userId).Returns(user);
        _userManagerMock.GetPasskeysAsync(user).Returns(new List<UserPasskeyInfo> { passkey });
        _userManagerMock.AddOrUpdatePasskeyAsync(user, Arg.Any<UserPasskeyInfo>()).Returns(IdentityResult.Success);

        await using var tester = new MassTransitTester(cfg =>
        {
            cfg.AddConsumer<RenamePasskeyConsumer>();
            cfg.AddSingleton(_userManagerMock);
            cfg.AddSingleton(_loggerMock);
        });

        // Act
        await tester.TestCommand<RenamePasskey, RenamePasskeyConsumer>(request);

        // Assert
        var message = await tester.Harness.Published
            .SelectAsync<PasskeyRenamingCompleted>(TestContext.Current.CancellationToken)
            .FirstOrDefault();
        message.Should().NotBeNull();
        message.Context.Message.ErrorInfo.Should().BeNull();
        await _userManagerMock.Received(1)
            .AddOrUpdatePasskeyAsync(user,
                Arg.Is<UserPasskeyInfo>(p => p.Name == existingName.ToUpperInvariant()));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Should_return_an_error_when_new_name_is_empty_or_whitespace(string? newName)
    {
        // Arrange
        const string userId = "test-user-id";
        var user = new SuiteUser
        {
            Id = userId,
            UserName = "testuser"
        };
        var request = new RenamePasskey
        {
            UserId = userId,
            PasskeyId = PasskeyIdConverter.EncodePasskeyId([1, 2, 3]),
            NewName = newName!
        };

        _userManagerMock.FindByIdAsync(userId).Returns(user);

        await using var tester = new MassTransitTester(cfg =>
        {
            cfg.AddConsumer<RenamePasskeyConsumer>();
            cfg.AddSingleton(_userManagerMock);
            cfg.AddSingleton(_loggerMock);
        });

        // Act
        await tester.TestCommand<RenamePasskey, RenamePasskeyConsumer>(request);

        // Assert
        var message = await tester.Harness.Published
            .SelectAsync<PasskeyRenamingCompleted>(TestContext.Current.CancellationToken)
            .FirstOrDefault();
        message.Should().NotBeNull();
        message.Context.Message.ErrorInfo.Should().NotBeNull();
        message.Context.Message.ErrorInfo!.ErrorCode.Should().Be((int)PasskeyError.NameInvalid);
        await _userManagerMock.DidNotReceive()
            .GetPasskeysAsync(Arg.Any<SuiteUser>());
    }

    [Fact]
    public async Task Should_return_an_error_when_new_name_is_already_used_by_another_passkey()
    {
        // Arrange
        const string userId = "test-user-id";
        var credentialId = new byte[] { 1, 2, 3 };
        var otherCredentialId = new byte[] { 4, 5, 6 };
        var user = new SuiteUser
        {
            Id = userId,
            UserName = "testuser"
        };
        var targetPasskey = new UserPasskeyInfo(credentialId,
            [],
            DateTimeOffset.UtcNow,
            0,
            null,
            true,
            true,
            true,
            [],
            []) { Name = "phone" };
        var otherPasskey = new UserPasskeyInfo(otherCredentialId,
            [],
            DateTimeOffset.UtcNow,
            0,
            null,
            true,
            true,
            true,
            [],
            []) { Name = "tablet" };
        var request = new RenamePasskey
        {
            UserId = userId,
            PasskeyId = PasskeyIdConverter.EncodePasskeyId(credentialId),
            NewName = "TABLET"
        };

        _userManagerMock.FindByIdAsync(userId).Returns(user);
        _userManagerMock.GetPasskeysAsync(user)
            .Returns(new List<UserPasskeyInfo> { targetPasskey, otherPasskey });

        await using var tester = new MassTransitTester(cfg =>
        {
            cfg.AddConsumer<RenamePasskeyConsumer>();
            cfg.AddSingleton(_userManagerMock);
            cfg.AddSingleton(_loggerMock);
        });

        // Act
        await tester.TestCommand<RenamePasskey, RenamePasskeyConsumer>(request);

        // Assert
        var message = await tester.Harness.Published
            .SelectAsync<PasskeyRenamingCompleted>(TestContext.Current.CancellationToken)
            .FirstOrDefault();
        message.Should().NotBeNull();
        message.Context.Message.ErrorInfo.Should().NotBeNull();
        message.Context.Message.ErrorInfo!.ErrorCode.Should().Be((int)PasskeyError.NameAlreadyInUse);
        await _userManagerMock.DidNotReceive()
            .AddOrUpdatePasskeyAsync(Arg.Any<SuiteUser>(), Arg.Any<UserPasskeyInfo>());
    }

    [Fact]
    public async Task Should_return_an_error_when_new_name_exceeds_maximum_length()
    {
        // Arrange
        const string userId = "test-user-id";
        var user = new SuiteUser
        {
            Id = userId,
            UserName = "testuser"
        };
        var request = new RenamePasskey
        {
            UserId = userId,
            PasskeyId = PasskeyIdConverter.EncodePasskeyId([1, 2, 3]),
            NewName = new string('a', 201) // Exceeds 200 character limit
        };

        _userManagerMock.FindByIdAsync(userId).Returns(user);

        await using var tester = new MassTransitTester(cfg =>
        {
            cfg.AddConsumer<RenamePasskeyConsumer>();
            cfg.AddSingleton(_userManagerMock);
            cfg.AddSingleton(_loggerMock);
        });

        // Act
        await tester.TestCommand<RenamePasskey, RenamePasskeyConsumer>(request);

        // Assert
        var message = await tester.Harness.Published
            .SelectAsync<PasskeyRenamingCompleted>(TestContext.Current.CancellationToken)
            .FirstOrDefault();
        message.Should().NotBeNull();
        message.Context.Message.ErrorInfo.Should().NotBeNull();
        message.Context.Message.ErrorInfo!.ErrorCode.Should().Be((int)PasskeyError.NameInvalid);
        await _userManagerMock.DidNotReceive()
            .GetPasskeysAsync(Arg.Any<SuiteUser>());
    }
}
