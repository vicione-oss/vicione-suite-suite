using AwesomeAssertions;
using Core.OS.Modules;
using Core.OS.UserManagement.Configuration;
using Core.OS.UserManagement.Consumers;
using Core.OS.UserManagement.Extensions;
using Core.Shared.UserManagement.Commands;
using Core.Shared.UserManagement.Contracts;
using Core.Shared.UserManagement.Events;
using MassTransit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using NSubstitute;
using Sdk.Testing.Backend;
using Xunit;

namespace Core.OS.Tests.UserManagement.Consumers;

public class CreateUserConsumerTests
{
    private readonly Action<IBusRegistrationConfigurator> _configureServices;

    public CreateUserConsumerTests()
        => _configureServices = cfg =>
        {
            cfg.AddConsumer<CreateUserConsumer>();
            cfg.AddUserDbContextsInMemory();
            cfg.AddSingleton(Substitute.For<IModuleHost>());
            cfg.AddSingleton(Options.Create(new UserManagementOptions { SeedTestUsers = true }));
            cfg.AddUserManagement();
        };

    [Fact]
    public async Task Command_should_be_consumed()
    {
        // Arrange
        await using var tester = new MassTransitTester(_configureServices);
        await using var scope = tester.Services.CreateAsyncScope();
        await scope.ServiceProvider.SeedUsersAndRoles(CancellationToken.None);
        var command = new CreateUser(new UserProfile
        {
            UserName = new UserName("test"),
            Email = "test@test.com"
        });

        // Act/Assert
        await tester.TestCommand<CreateUser, CreateUserConsumer>(command);
    }

    [Fact]
    public async Task Command_should_publish_user_changed_event()
    {
        // Arrange
        await using var tester = new MassTransitTester(_configureServices);
        await using var scope = tester.Services.CreateAsyncScope();
        await scope.ServiceProvider.SeedUsersAndRoles(CancellationToken.None);
        await scope.ServiceProvider.SeedTestRole();
        var userProfile = new UserProfile
        {
            UserName = new UserName("username"),
            Email = "test@test.com",
            Roles = [TestExtensions.TestRoleName],
            NewPassword = SeedingExtensions.Eddy.Password
        };
        var command = new CreateUser(userProfile);

        // Act/Assert
        var response = await tester.TestCommand<CreateUser, CreateUserConsumer, UserCreatedEvent>(command);
        response.Should().BeEquivalentTo(new UserCreatedEvent(userProfile) { CorrelationId = command.CorrelationId });
    }

    [Fact]
    public async Task Consume_should_publish_user_error_event_if_user_exists()
    {
        // Arrange
        await using var tester = new MassTransitTester(_configureServices);
        await using var scope = tester.Services.CreateAsyncScope();
        await scope.ServiceProvider.SeedUsersAndRoles(CancellationToken.None);
        var user = SeedingExtensions.Eddy.ToUserProfile(true);
        var command = new CreateUser(user);

        // Act/Assert
        var response = await tester.TestCommand<CreateUser, CreateUserConsumer, UserCreatedEvent>(command);
        response.ErrorInfo.Should().NotBeNull();
        response.ErrorInfo.ErrorCode.Should().Be(UserErrorCodes.CreateFailedAlreadyExists);
        response.UserProfile.UserName.Should().Be(user.UserName);
    }

    [Fact]
    public async Task Consume_should_publish_user_error_event_if_password_is_missing()
    {
        // Arrange
        await using var tester = new MassTransitTester(_configureServices);
        await using var scope = tester.Services.CreateAsyncScope();
        await scope.ServiceProvider.SeedUsersAndRoles(CancellationToken.None);
        await scope.ServiceProvider.SeedTestRole();
        var user = new UserProfile
        {
            UserName = new UserName("username"),
            Email = "test@test.com",
            Roles = [TestExtensions.TestRoleName],
        };
        var command = new CreateUser(user);

        // Act/Assert
        var response = await tester.TestCommand<CreateUser, CreateUserConsumer, UserCreatedEvent>(command);
        response.ErrorInfo.Should().NotBeNull();
        response.ErrorInfo.ErrorCode.Should().Be(UserErrorCodes.CreateFailedMissingPw);
        response.UserProfile.UserName.Should().Be(user.UserName);
    }

    [Fact]
    public async Task Consume_should_publish_user_error_event_if_password_is_invalid()
    {
        // Arrange
        await using var tester = new MassTransitTester(_configureServices);
        await using var scope = tester.Services.CreateAsyncScope();
        await scope.ServiceProvider.SeedUsersAndRoles(CancellationToken.None);
        await scope.ServiceProvider.SeedTestRole();

        var user = new UserProfile
        {
            UserName = new UserName("username"),
            Email = "test@test.com",
            Roles = [TestExtensions.TestRoleName],
            NewPassword = "123"
        };
        var command = new CreateUser(user);

        // Act/Assert
        var response = await tester.TestCommand<CreateUser, CreateUserConsumer, UserCreatedEvent>(command);
        response.ErrorInfo.Should().NotBeNull();
        response.ErrorInfo.ErrorCode.Should().Be(UserErrorCodes.CreateFailed);
        response.UserProfile.UserName.Should().Be(user.UserName);
    }
}
