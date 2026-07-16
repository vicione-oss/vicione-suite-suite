using AwesomeAssertions;
using Core.OS.Modules;
using Core.OS.UserManagement.Configuration;
using Core.OS.UserManagement.Consumers;
using Core.OS.UserManagement.Extensions;
using Core.Shared.UserManagement;
using Core.Shared.UserManagement.Contracts;
using Core.Shared.UserManagement.Events;
using MassTransit;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
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
    public async Task Should_be_consumed()
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
    public async Task Should_publish_role_deleted_event()
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
    public async Task Should_publish_idempotent_success_when_role_already_deleted()
    {
        // Arrange
        await using var tester = new MassTransitTester(_configureServices);
        await using var scope = tester.Services.CreateAsyncScope();
        await scope.ServiceProvider.SeedUsersAndRoles(CancellationToken.None);
        var role = new Role { Name = "Test" };
        var command = new DeleteRole(role);

        // Act — ADR-002: redelivery after successful delete must publish completion without error.
        var response = await tester.TestCommand<DeleteRole, DeleteRoleConsumer, RoleDeletedEvent>(command);

        // Assert
        response.ErrorInfo.Should().BeNull();
        response.Role.Name.Should().Be(role.Name);
    }

    [Fact]
    public async Task Should_not_delete_default_roles()
    {
        // Arrange
        await using var tester = new MassTransitTester(_configureServices);
        await using var scope = tester.Services.CreateAsyncScope();
        await scope.ServiceProvider.SeedUsersAndRoles(CancellationToken.None);
        var role = new Role { Name = AuthorizationConstants.AdminRoleName };
        var command = new DeleteRole(role);

        // Act
        var response = await tester.TestCommand<DeleteRole, DeleteRoleConsumer, RoleDeletedEvent>(command);

        // Assert
        response.ErrorInfo.Should().NotBeNull();
        response.ErrorInfo.ErrorCode.Should().Be(UserErrorCodes.DeleteFailed);
        response.Role.Name.Should().Be(role.Name);
    }

    [Fact]
    public async Task Should_publish_delete_failed_error_on_unexpected_exception()
    {
        // Arrange — bypass the MassTransit harness to inject a throwing store directly.
        const string roleName = "TestRole";
        var suiteRole = new SuiteRole(roleName);

        var roleStore = Substitute.For<IRoleStore<SuiteRole>>();
        roleStore.FindByNameAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<SuiteRole?>(suiteRole));
        roleStore.DeleteAsync(Arg.Any<SuiteRole>(), Arg.Any<CancellationToken>())
            .Returns(_ => Task.FromException<IdentityResult>(new InvalidOperationException("Simulated store failure")));

        using var roleManager = new RoleManager<SuiteRole>(
            roleStore,
            [],
            Substitute.For<ILookupNormalizer>(),
            new IdentityErrorDescriber(),
            NullLogger<RoleManager<SuiteRole>>.Instance);

        var consumer = new DeleteRoleConsumer(roleManager, NullLogger<DeleteRoleConsumer>.Instance);

        var context = Substitute.For<ConsumeContext<DeleteRole>>();
        context.Message.Returns(new DeleteRole(new Role { Name = roleName }));
        context.CancellationToken.Returns(CancellationToken.None);
        context.Publish(Arg.Any<RoleDeletedEvent>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        // Act
        await consumer.Consume(context);

        // Assert
        await context.Received(1).Publish(
            Arg.Is<RoleDeletedEvent>(e => e!.ErrorInfo != null && e.ErrorInfo.ErrorCode == RoleErrorCodes.DeleteFailed),
            Arg.Any<CancellationToken>());
    }
}

