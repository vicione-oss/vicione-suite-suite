using AwesomeAssertions;
using Core.OS.Modules;
using Core.OS.UserManagement.Configuration;
using Core.OS.UserManagement.Consumers;
using Core.OS.UserManagement.Extensions;
using Core.Shared.UserManagement.Contracts;
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

public class UpdateRoleConsumerTests
{
    private readonly Action<IBusRegistrationConfigurator> _configureServices;

    public UpdateRoleConsumerTests()
        => _configureServices = cfg =>
        {
            cfg.AddConsumer<UpdateRoleConsumer>();
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
        var command = new UpdateRole(new() { Name = "Test" });

        // Act/Assert
        await tester.TestCommand<UpdateRole, UpdateRoleConsumer>(command);
    }

    [Fact]
    public async Task Command_should_publish_role_changed_event()
    {
        // Arrange
        await using var tester = new MassTransitTester(_configureServices);
        await using var scope = tester.Services.CreateAsyncScope();
        await scope.ServiceProvider.SeedUsersAndRoles(CancellationToken.None);
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<SuiteRole>>();
        var role = new SuiteRole("Test");

        await roleManager.CreateAsync(role);
        await roleManager.AddClaimAsync(role, new("Test", "Test"));
        await roleManager.AddClaimAsync(role, new("Test1", "Test1"));

        var command = new UpdateRole(new() { Name = "Test", Description = "Test", Claims = [new() { Type = "Test", Value = "Test" }, new() { Type = "Test2", Value = "Test2" }] });

        // Act
        var response = await tester.TestCommand<UpdateRole, UpdateRoleConsumer, RoleUpdatedEvent>(command);

        // Assert
        response.Role.Description.Should().BeEquivalentTo(role.Description);
    }

    [Fact]
    public async Task Should_not_update_role_if_role_is_default()
    {
        // Arrange
        await using var tester = new MassTransitTester(_configureServices);
        await using var scope = tester.Services.CreateAsyncScope();
        await scope.ServiceProvider.SeedUsersAndRoles(CancellationToken.None);

        var role = new Role() { Name = SeedingExtensions.AdminRoleName };
        var command = new UpdateRole(role);

        // Act
        var response = await tester.TestCommand<UpdateRole, UpdateRoleConsumer, RoleUpdatedEvent>(command);

        // Assert
        response.ErrorInfo.Should().NotBeNull();
        response.ErrorInfo.ErrorCode.Should().Be(RoleErrorCodes.UpdateFailed);
        response.Role.Name.Should().Be(SeedingExtensions.AdminRoleName);
    }

    [Fact]
    public async Task Consume_should_publish_role_error_event_for_unknown_role()
    {
        // Arrange
        await using var tester = new MassTransitTester(_configureServices);
        await using var scope = tester.Services.CreateAsyncScope();
        await scope.ServiceProvider.SeedUsersAndRoles(CancellationToken.None);
        var role = new Role() { Name = "Test" };
        var command = new UpdateRole(role);

        // Act
        var response = await tester.TestCommand<UpdateRole, UpdateRoleConsumer, RoleUpdatedEvent>(command);

        // Assert
        response.ErrorInfo.Should().NotBeNull();
        response.ErrorInfo.ErrorCode.Should().Be(RoleErrorCodes.UpdateFailedNotFound);
        response.Role.Name.Should().Be(role.Name);
    }
}
