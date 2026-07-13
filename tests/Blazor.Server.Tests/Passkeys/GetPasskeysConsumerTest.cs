using AwesomeAssertions;
using Blazor.Server.Backend.Passkeys;
using Core.Shared.Passkeys;
using Core.Shared.Passkeys.Requests;
using Core.Shared.UserManagement.Contracts;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Sdk.Testing.Backend;
using Xunit;

namespace Blazor.Server.Tests.Passkeys;

public sealed class GetPasskeysConsumerTest
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

    private readonly ILogger<GetPasskeysConsumer> _loggerMock =
        Substitute.For<ILogger<GetPasskeysConsumer>>();

    [Fact]
    public async Task Should_return_passkeys_for_a_known_user()
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
            []) { Name = "phone" };

        _userManagerMock.FindByIdAsync(userId).Returns(user);
        _userManagerMock.GetPasskeysAsync(user).Returns(new List<UserPasskeyInfo> { passkey });

        await using var tester = new MassTransitTester(cfg =>
        {
            cfg.AddConsumer<GetPasskeysConsumer>();
            cfg.AddSingleton(_userManagerMock);
            cfg.AddSingleton(_loggerMock);
        });

        // Act
        var response = await tester.TestRequest<GetPasskeysResponse, GetPasskeys>(new GetPasskeys(userId));

        // Assert
        response.RequestError.Should().BeNull();
        var dto = response.Passkeys.Should().ContainSingle().Subject;
        dto.Id.Should().Be(PasskeyIdConverter.EncodePasskeyId(credentialId));
        dto.Name.Should().Be("phone");
    }

    [Fact]
    public async Task Should_return_empty_list_when_user_has_no_passkeys()
    {
        // Arrange
        const string userId = "test-user-id";
        var user = new SuiteUser
        {
            Id = userId,
            UserName = "testuser"
        };

        _userManagerMock.FindByIdAsync(userId).Returns(user);
        _userManagerMock.GetPasskeysAsync(user).Returns(new List<UserPasskeyInfo>());

        await using var tester = new MassTransitTester(cfg =>
        {
            cfg.AddConsumer<GetPasskeysConsumer>();
            cfg.AddSingleton(_userManagerMock);
            cfg.AddSingleton(_loggerMock);
        });

        // Act
        var response = await tester.TestRequest<GetPasskeysResponse, GetPasskeys>(new GetPasskeys(userId));

        // Assert
        response.RequestError.Should().BeNull();
        response.Passkeys.Should().BeEmpty();
    }

    [Fact]
    public async Task Should_return_an_error_when_user_is_unknown()
    {
        // Arrange
        const string userId = "unknown-user-id";
        _userManagerMock.FindByIdAsync(userId).Returns((SuiteUser?)null);

        await using var tester = new MassTransitTester(cfg =>
        {
            cfg.AddConsumer<GetPasskeysConsumer>();
            cfg.AddSingleton(_userManagerMock);
            cfg.AddSingleton(_loggerMock);
        });

        // Act
        var response = await tester.TestRequest<GetPasskeysResponse, GetPasskeys>(new GetPasskeys(userId));

        // Assert
        response.Passkeys.Should().BeEmpty();
        response.RequestError.Should().NotBeNull();
        response.RequestError!.ErrorCode.Should().Be((int)PasskeyError.UserNotFound);
        await _userManagerMock.DidNotReceive().GetPasskeysAsync(Arg.Any<SuiteUser>());
    }

    [Fact]
    public async Task Should_return_an_error_when_user_manager_throws()
    {
        // Arrange
        const string userId = "test-user-id";
        var user = new SuiteUser
        {
            Id = userId,
            UserName = "testuser"
        };

        _userManagerMock.FindByIdAsync(userId).Returns(user);
        _userManagerMock.GetPasskeysAsync(user).Returns<IList<UserPasskeyInfo>>(_ => throw new InvalidOperationException("store unavailable"));

        await using var tester = new MassTransitTester(cfg =>
        {
            cfg.AddConsumer<GetPasskeysConsumer>();
            cfg.AddSingleton(_userManagerMock);
            cfg.AddSingleton(_loggerMock);
        });

        // Act
        var response = await tester.TestRequest<GetPasskeysResponse, GetPasskeys>(new GetPasskeys(userId));

        // Assert
        response.Passkeys.Should().BeEmpty();
        response.RequestError.Should().NotBeNull();
        response.RequestError!.ErrorCode.Should().Be((int)PasskeyError.UnknownError);
    }
}
