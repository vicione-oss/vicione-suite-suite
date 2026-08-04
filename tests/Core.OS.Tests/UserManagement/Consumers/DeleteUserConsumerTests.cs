using Core.OS.Modules;
using Core.OS.UserManagement.Configuration;
using Core.OS.UserManagement.Consumers;
using Core.OS.UserManagement.Extensions;
using Core.Shared.UserManagement.Commands;
using Core.Shared.UserManagement.Contracts;
using Core.Shared.UserManagement.Events;
using MassTransit;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Sdk.Testing.Backend;

namespace Core.OS.Tests.UserManagement.Consumers;

public class DeleteUserConsumerTests
{
    private readonly Action<IBusRegistrationConfigurator> _configureServices;

    public DeleteUserConsumerTests()
        => _configureServices = cfg =>
        {
            cfg.AddConsumer<DeleteUserConsumer>();
            cfg.AddUserDbContextsInMemory();
            cfg.AddSingleton(Substitute.For<IModuleHost>());
            cfg.AddSingleton(Options.Create(new UserManagementOptions { SeedTestUsers = true }));
            cfg.AddUserManagement();
        };

    [Fact]
    public async Task Should_be_consumed()
    {
        // Arrange
        await using var tester = new MassTransitTester(_configureServices);
        await using var scope = tester.Services.CreateAsyncScope();
        await scope.ServiceProvider.SeedUsersAndRoles(CancellationToken.None);
        var command = new DeleteUser(new UserProfile
        {
            UserName = new UserName("test"),
            Email = "test@test.com"
        });

        // Act/Assert
        await tester.TestCommand<DeleteUser, DeleteUserConsumer>(command);
    }

    [Fact]
    public async Task Should_publish_user_changed_event()
    {
        // Arrange
        await using var tester = new MassTransitTester(_configureServices);
        await using var scope = tester.Services.CreateAsyncScope();
        await scope.ServiceProvider.SeedUsersAndRoles(CancellationToken.None);
        var userProfile = SeedingExtensions.Bob.ToUserProfile();
        var command = new DeleteUser(userProfile);

        // Act/Assert
        var response = await tester.TestCommand<DeleteUser, DeleteUserConsumer, UserDeletedEvent>(command);
        response.Should().BeEquivalentTo(new UserDeletedEvent(userProfile) { CorrelationId = command.CorrelationId });
    }

    [Fact]
    public async Task Should_publish_idempotent_success_when_user_already_deleted()
    {
        // Arrange
        await using var tester = new MassTransitTester(_configureServices);
        await using var scope = tester.Services.CreateAsyncScope();
        await scope.ServiceProvider.SeedUsersAndRoles(CancellationToken.None);
        var user = new UserProfile
        {
            UserName = new UserName("Unknown"),
            Email = "test@test.com",
            Roles = []
        };
        var command = new DeleteUser(user);

        // Act/Assert — ADR-002: redelivery after successful delete must publish completion without error.
        var response = await tester.TestCommand<DeleteUser, DeleteUserConsumer, UserDeletedEvent>(command);
        response.ErrorInfo.Should().BeNull();
        response.UserProfile.UserName.Should().Be(user.UserName);
    }

    [Fact]
    public async Task Should_delete_sys_admin_if_there_are_others_left()
    {
        // Arrange
        await using var tester = new MassTransitTester(_configureServices);
        await using var scope = tester.Services.CreateAsyncScope();
        await scope.ServiceProvider.SeedUsersAndRoles(CancellationToken.None);

        var userProfile = SeedingExtensions.Admin.ToUserProfile();
        var command = new DeleteUser(userProfile);

        // Act/Assert
        var response = await tester.TestCommand<DeleteUser, DeleteUserConsumer, UserDeletedEvent>(command);
        response.Should().BeEquivalentTo(new UserDeletedEvent(userProfile) { CorrelationId = command.CorrelationId });
    }

    [Fact]
    public async Task Should_publish_error_on_attempt_deleting_last_sys_admin()
    {
        // Arrange
        await using var tester = new MassTransitTester(_configureServices);
        await using var scope = tester.Services.CreateAsyncScope();
        await scope.ServiceProvider.SeedUsersAndRoles(CancellationToken.None);

        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<SuiteUser>>();
        var eddy = await userManager.FindByNameAsync(SeedingExtensions.Eddy.UserName);
        await userManager.DeleteAsync(eddy!);

        var userProfile = SeedingExtensions.Admin.ToUserProfile();
        var command = new DeleteUser(userProfile);

        // Act/Assert
        var response = await tester.TestCommand<DeleteUser, DeleteUserConsumer, UserDeletedEvent>(command);
        response.ErrorInfo.Should().NotBeNull();
        response.ErrorInfo.ErrorCode.Should().Be(UserErrorCodes.SystemAdminLockout);
        response.UserProfile.UserName.Should().Be(command.UserProfile.UserName);
    }
}
