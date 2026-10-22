using Core.OS.DbContext;
using Core.OS.Modules;
using Core.OS.UserManagement.Configuration;
using Core.OS.UserManagement.Consumers;
using Core.OS.UserManagement.Extensions;
using Core.Shared.UserManagement;
using Core.Shared.UserManagement.Commands;
using Core.Shared.UserManagement.Contracts;
using Core.Shared.UserManagement.Events;
using MassTransit;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Sdk.Testing.Backend;

namespace Core.OS.Tests.UserManagement.Consumers;

public class UpdateUserConsumerTests
{
    private readonly Action<IBusRegistrationConfigurator> _configureServices;

    public UpdateUserConsumerTests()
        => _configureServices = cfg =>
        {
            cfg.AddConsumer<UpdateUserConsumer>();
            cfg.AddUserDbContextsInMemory();
            // An administrator sets another user's password through a reset token; the shared test Identity setup
            // registers no token providers.
            cfg.AddDataProtection();
            new IdentityBuilder(typeof(SuiteUser), cfg).AddDefaultTokenProviders();
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
    public async Task Should_publish_user_changed_event()
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
    public async Task Should_publish_user_error_event_for_unknown_user()
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
    public async Task Should_publish_user_error_event_for_invalid_password_change()
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
    public async Task Should_publish_user_error_event_for_incorrect_password()
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

    [Fact]
    public async Task Should_not_save_profile_when_current_password_is_incorrect()
    {
        // Arrange
        await using var tester = new MassTransitTester(_configureServices);
        await using var scope = tester.Services.CreateAsyncScope();
        await scope.ServiceProvider.SeedUsersAndRoles(CancellationToken.None);
        var user = SeedingExtensions.Eddy.ToUserProfile();
        user.Email = "changed@example.com";
        user.CurrentPassword = "Up2Good!!";
        user.NewPassword = "%Up2noG00d$$!";
        var command = new UpdateUser(user);

        // Act
        var response = await tester.TestCommand<UpdateUser, UpdateUserConsumer, UserUpdatedEvent>(command);

        // Assert
        response.ErrorInfo.Should().NotBeNull();
        response.ErrorInfo.ErrorCode.Should().Be(UserErrorCodes.UpdateFailedPassword);
        var storedUser = await FindStoredUser(scope.ServiceProvider, SeedingExtensions.Eddy.UserName);
        storedUser.Email.Should().Be(SeedingExtensions.Eddy.Email);
    }

    [Fact]
    public async Task Should_keep_password_when_administrator_sets_invalid_password()
    {
        // Arrange
        await using var tester = new MassTransitTester(_configureServices);
        await using var scope = tester.Services.CreateAsyncScope();
        await scope.ServiceProvider.SeedUsersAndRoles(CancellationToken.None);
        var user = SeedingExtensions.Eddy.ToUserProfile();
        user.NewPassword = "Too short";
        var command = new UpdateUser(user, SeedingExtensions.Admin.UserName);

        // Act
        var response = await tester.TestCommand<UpdateUser, UpdateUserConsumer, UserUpdatedEvent>(command);

        // Assert
        response.ErrorInfo.Should().NotBeNull();
        response.ErrorInfo.ErrorCode.Should().Be(UserErrorCodes.UpdateFailedPassword);
        var storedUser = await FindStoredUser(scope.ServiceProvider, SeedingExtensions.Eddy.UserName);
        storedUser.PasswordHash.Should().NotBeNull();
    }

    [Fact]
    public async Task Should_set_password_when_administrator_changes_password_of_another_user()
    {
        // Arrange
        await using var tester = new MassTransitTester(_configureServices);
        await using var scope = tester.Services.CreateAsyncScope();
        await scope.ServiceProvider.SeedUsersAndRoles(CancellationToken.None);
        var user = SeedingExtensions.Eddy.ToUserProfile();
        user.NewPassword = "%Up2noG00d$$!";
        var command = new UpdateUser(user, SeedingExtensions.Admin.UserName);

        // Act
        var response = await tester.TestCommand<UpdateUser, UpdateUserConsumer, UserUpdatedEvent>(command);

        // Assert
        response.ErrorInfo.Should().BeNull();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<SuiteUser>>();
        var storedUser = await FindStoredUser(scope.ServiceProvider, SeedingExtensions.Eddy.UserName);
        (await userManager.CheckPasswordAsync(storedUser, "%Up2noG00d$$!")).Should().BeTrue();
    }

    [Fact]
    public async Task Should_publish_user_error_event_for_duplicate_email()
    {
        // Arrange
        await using var tester = new MassTransitTester(ConfigureServicesWithUniqueEmail);
        await using var scope = tester.Services.CreateAsyncScope();
        await scope.ServiceProvider.SeedUsersAndRoles(CancellationToken.None);
        var user = SeedingExtensions.Eddy.ToUserProfile();
        user.Email = SeedingExtensions.Admin.Email;
        var command = new UpdateUser(user);

        // Act
        var response = await tester.TestCommand<UpdateUser, UpdateUserConsumer, UserUpdatedEvent>(command);

        // Assert
        response.ErrorInfo.Should().NotBeNull();
        response.ErrorInfo.ErrorCode.Should().Be(UserErrorCodes.UpdateFailed);
        var storedUser = await FindStoredUser(scope.ServiceProvider, SeedingExtensions.Eddy.UserName);
        storedUser.Email.Should().Be(SeedingExtensions.Eddy.Email);
    }

    [Fact]
    public async Task Should_not_change_password_when_email_is_duplicate()
    {
        // Arrange
        await using var tester = new MassTransitTester(ConfigureServicesWithUniqueEmail);
        await using var scope = tester.Services.CreateAsyncScope();
        await scope.ServiceProvider.SeedUsersAndRoles(CancellationToken.None);
        var passwordHashBefore = (await FindStoredUser(scope.ServiceProvider, SeedingExtensions.Eddy.UserName)).PasswordHash;
        var user = SeedingExtensions.Eddy.ToUserProfile();
        user.Email = SeedingExtensions.Admin.Email;
        user.CurrentPassword = SeedingExtensions.Eddy.Password;
        user.NewPassword = "%Up2noG00d$$!";
        var command = new UpdateUser(user);

        // Act
        var response = await tester.TestCommand<UpdateUser, UpdateUserConsumer, UserUpdatedEvent>(command);

        // Assert
        response.ErrorInfo.Should().NotBeNull();
        response.ErrorInfo.ErrorCode.Should().Be(UserErrorCodes.UpdateFailed);
        var storedUser = await FindStoredUser(scope.ServiceProvider, SeedingExtensions.Eddy.UserName);
        storedUser.PasswordHash.Should().Be(passwordHashBefore);
    }

    /// <summary>
    /// Production sets <see cref="UserOptions.RequireUniqueEmail"/>; the shared test Identity setup does not.
    /// </summary>
    private void ConfigureServicesWithUniqueEmail(IBusRegistrationConfigurator cfg)
    {
        _configureServices(cfg);
        cfg.Configure<IdentityOptions>(options => options.User.RequireUniqueEmail = true);
    }

    /// <summary>
    /// Reads past the change tracker: the test context is a singleton, so its tracked user still holds a rejected edit.
    /// </summary>
    private static Task<SuiteUser> FindStoredUser(IServiceProvider services, string userName)
        => services.GetRequiredService<UserDbContext>().Users.AsNoTracking().SingleAsync(u => u.UserName == userName);
}
