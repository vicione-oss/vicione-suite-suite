using AwesomeAssertions;
using Core.OS.Modules;
using Core.OS.UserManagement.Configuration;
using Core.OS.UserManagement.Consumers;
using Core.OS.UserManagement.Extensions;
using Core.Shared.UserManagement;
using Core.Shared.UserManagement.Commands;
using Core.Shared.UserManagement.Contracts;
using Core.Shared.UserManagement.Events;
using MassTransit;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using NSubstitute;
using Sdk.Testing.Backend;
using Xunit;

namespace Core.OS.Tests.UserManagement.Consumers;

public class UpdateUserConsumerTests
{
    private readonly Action<IBusRegistrationConfigurator> _configureServices;

    public UpdateUserConsumerTests()
        => _configureServices = cfg =>
        {
            cfg.AddConsumer<UpdateUserConsumer>();
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
        var command = new UpdateUser(
            new UserProfile
            {
                UserName = new UserName("test"),
                Email = "test@test.com"
            });

        // Act/Assert
        await tester.TestCommand<UpdateUser, UpdateUserConsumer>(command);
    }

    [Fact]
    public async Task Command_should_publish_user_changed_event()
    {
        // Arrange
        await using var tester = new MassTransitTester(_configureServices);
        await using var scope = tester.Services.CreateAsyncScope();
        await scope.ServiceProvider.SeedUsersAndRoles(CancellationToken.None);
        var userProfile = new UserProfile
        {
            UserName = new UserName(SeedingExtensions.Eddy.UserName),
            Email = "test@test.com",
            Roles = [SeedingExtensions.Eddy.AccessLevel.ToString()]
        };
        var command = new UpdateUser(userProfile);

        // Act/Assert
        var response = await tester.TestCommand<UpdateUser, UpdateUserConsumer, UserUpdatedEvent>(command);
        response.UserProfile.Should().BeEquivalentTo(userProfile);
    }

    [Fact]
    public async Task Should_remove_sysadmin_role_if_other_sysadmin_users_left()
    {
        // Arrange
        await using var tester = new MassTransitTester(_configureServices);
        await using var scope = tester.Services.CreateAsyncScope();
        await scope.ServiceProvider.SeedUsersAndRoles(CancellationToken.None);
        var userProfile = new UserProfile
        {
            UserName = new UserName(SeedingExtensions.Eddy.UserName),
            Email = "test@test.com",
            Roles = [SeedingExtensions.Eddy.AccessLevel.ToString()]
        };
        var command = new UpdateUser(userProfile);

        // Act/Assert
        var response = await tester.TestCommand<UpdateUser, UpdateUserConsumer, UserUpdatedEvent>(command);
        response.UserProfile.Should().BeEquivalentTo(userProfile);
    }

    [Fact]
    public async Task Should_not_remove_sysadmin_role_if_no_other_sysadmin_users_left()
    {
        // Arrange
        await using var tester = new MassTransitTester(_configureServices);
        await using var scope = tester.Services.CreateAsyncScope();
        await scope.ServiceProvider.SeedUsersAndRoles(CancellationToken.None);

        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<SuiteUser>>();
        var eddy = await userManager.FindByNameAsync(SeedingExtensions.Eddy.UserName);
        await userManager.DeleteAsync(eddy!);

        var userProfile = new UserProfile
        {
            UserName = new UserName(SeedingExtensions.Admin.UserName),
            Email = "test@test.com",
            Roles = [SeedingExtensions.Admin.AccessLevel.ToString()]
        };
        var command = new UpdateUser(userProfile);

        // Act/Assert
        var response = await tester.TestCommand<UpdateUser, UpdateUserConsumer, UserUpdatedEvent>(command);
        response.UserProfile.Roles.Should().ContainSingle(k => k == AuthorizationConstants.AdminRoleName);
    }

    [Fact]
    public async Task Consume_should_publish_user_error_event_for_unknown_user()
    {
        // Arrange
        await using var tester = new MassTransitTester(_configureServices);
        await using var scope = tester.Services.CreateAsyncScope();
        await scope.ServiceProvider.SeedUsersAndRoles(CancellationToken.None);
        var user = new UserProfile
        {
            UserName = new UserName("Error"),
            Email = "test@test.com"
        };
        var command = new UpdateUser(user);

        // Act/Assert
        var response = await tester.TestCommand<UpdateUser, UpdateUserConsumer, UserUpdatedEvent>(command);
        response.ErrorInfo.Should().NotBeNull();
        response.ErrorInfo.ErrorCode.Should().Be(UserErrorCodes.UpdateFailedNotFound);
        response.UserProfile.UserName.Should().Be(user.UserName);
    }

    [Fact]
    public async Task Consume_should_publish_user_error_event_for_invalid_password_change()
    {
        // Arrange
        await using var tester = new MassTransitTester(_configureServices);
        await using var scope = tester.Services.CreateAsyncScope();
        await scope.ServiceProvider.SeedUsersAndRoles(CancellationToken.None);
        var user = SeedingExtensions.Eddy.ToUserProfile();
        user.CurrentPassword = SeedingExtensions.Eddy.Password;
        user.NewPassword = "Too short";
        var command = new UpdateUser(user);

        // Act/Assert
        var response = await tester.TestCommand<UpdateUser, UpdateUserConsumer, UserUpdatedEvent>(command);
        response.ErrorInfo.Should().NotBeNull();
        response.ErrorInfo.ErrorCode.Should().Be(UserErrorCodes.UpdateFailedPassword);
        response.UserProfile.UserName.Should().Be(user.UserName);
    }

    [Fact]
    public async Task Consume_should_publish_user_error_event_for_incorrect_password()
    {
        // Arrange
        await using var tester = new MassTransitTester(_configureServices);
        await using var scope = tester.Services.CreateAsyncScope();
        await scope.ServiceProvider.SeedUsersAndRoles(CancellationToken.None);
        var user = SeedingExtensions.Eddy.ToUserProfile();
        user.CurrentPassword = "Up2Good!!";
        user.NewPassword = "%Up2noG00d$$!";
        var command = new UpdateUser(user);

        // Act/Assert
        var response = await tester.TestCommand<UpdateUser, UpdateUserConsumer, UserUpdatedEvent>(command);
        response.ErrorInfo.Should().NotBeNull();
        response.ErrorInfo.ErrorCode.Should().Be(UserErrorCodes.UpdateFailedPassword);
        response.UserProfile.UserName.Should().Be(user.UserName);
    }
}
