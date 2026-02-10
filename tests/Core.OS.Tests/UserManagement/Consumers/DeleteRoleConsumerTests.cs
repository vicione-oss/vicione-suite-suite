using AwesomeAssertions;
using Core.OS.Modules;
using Core.OS.UserManagement.Configuration;
using Core.OS.UserManagement.Consumers;
using Core.OS.UserManagement.Extensions;
using Core.Shared.UserManagement.Contracts;
using Core.Shared.UserManagement.Events;
using MassTransit;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using NSubstitute;
using Sdk.Testing.Backend;
using Sdk.UserManagement.Commands;
using Sdk.UserManagement.Contracts;
using Sdk.UserManagement.Events;
using Xunit;

namespace Core.OS.Tests.UserManagement.Consumers;

public class DeleteRoleConsumerTests
{
    private readonly Action<IBusRegistrationConfigurator> _configureServices;

    public DeleteRoleConsumerTests()
        => _configureServices = cfg =>
        {
            cfg.AddConsumer<DeleteRoleConsumer>();
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
        var command = new DeleteRole(new() { Name = "Test" });

        // Act/Assert
        await tester.TestCommand<DeleteRole, DeleteRoleConsumer>(command);
    }

    [Fact]
    public async Task Command_should_publish_role_deleted_event()
    {
        // Arrange
        await using var tester = new MassTransitTester(_configureServices);
        await using var scope = tester.Services.CreateAsyncScope();
        await scope.ServiceProvider.SeedUsersAndRoles(CancellationToken.None);
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<SuiteRole>>();
        var roleName = "Test";

        var command = new DeleteRole(new() { Name = roleName });

        await roleManager.CreateAsync(new(roleName));

        // Act
        var response = await tester.TestCommand<DeleteRole, DeleteRoleConsumer, RoleDeletedEvent>(command);

        // Assert
        response.CorrelationId.Should().Be(command.CorrelationId);
        response.Role.Name.Should().Be(command.Role.Name);
    }

    [Fact]
    public async Task Consume_should_publish_role_error_event()
    {
        // Arrange
        await using var tester = new MassTransitTester(_configureServices);
        await using var scope = tester.Services.CreateAsyncScope();
        await scope.ServiceProvider.SeedUsersAndRoles(CancellationToken.None);
        var role = new Role { Name = "Test" };
        var command = new DeleteRole(role);

        // Act
        var response = await tester.TestCommand<DeleteRole, DeleteRoleConsumer, RoleErrorEvent>(command);

        // Assert
        response.ErrorInfo.Should().NotBeNull();
        response.ErrorInfo.ErrorCode.Should().Be(UserErrorEvent.DeleteFailedNotFound);
        response.Role.Name.Should().Be(role.Name);
    }

    [Fact]
    public async Task Should_not_delete_default_roles()
    {
        // Arrange
        await using var tester = new MassTransitTester(_configureServices);
        await using var scope = tester.Services.CreateAsyncScope();
        await scope.ServiceProvider.SeedUsersAndRoles(CancellationToken.None);
        var role = new Role { Name = SeedingExtensions.AdminRoleName };
        var command = new DeleteRole(role);

        // Act
        var response = await tester.TestCommand<DeleteRole, DeleteRoleConsumer, RoleErrorEvent>(command);

        // Assert
        response.ErrorInfo.Should().NotBeNull();
        response.ErrorInfo.ErrorCode.Should().Be(UserErrorEvent.DeleteFailed);
        response.Role.Name.Should().Be(role.Name);
    }
}

