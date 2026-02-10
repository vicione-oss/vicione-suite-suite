using AwesomeAssertions;
using Core.OS.Modules;
using Core.OS.UserManagement.Configuration;
using Core.OS.UserManagement.Consumers;
using Core.OS.UserManagement.Extensions;
using MassTransit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using NSubstitute;
using Sdk.Testing.Backend;
using Sdk.UserManagement.Commands;
using Sdk.UserManagement.Contracts;
using Sdk.UserManagement.Events;
using Xunit;

namespace Core.OS.Tests.UserManagement.Consumers;

public class CreateRoleConsumerTests
{
    private readonly Action<IBusRegistrationConfigurator> _configureServices;

    public CreateRoleConsumerTests()
        => _configureServices = cfg =>
        {
            cfg.AddConsumer<CreateRoleConsumer>();
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
        var command = new CreateRole(new Role { Name = "Test" });

        // Act/Assert
        await tester.TestCommand<CreateRole, CreateRoleConsumer>(command);
    }

    [Fact]
    public async Task Command_should_publish_role_changed_event()
    {
        // Arrange
        await using var tester = new MassTransitTester(_configureServices);
        await using var scope = tester.Services.CreateAsyncScope();
        await scope.ServiceProvider.SeedUsersAndRoles(CancellationToken.None);
        var role = new Role { Name = "Test", Claims = [new() { Type = "Test", Value = "Full" }] };
        var command = new CreateRole(role);

        // Act
        var response = await tester.TestCommand<CreateRole, CreateRoleConsumer, RoleCreatedEvent>(command);

        // Assert
        response.CorrelationId.Should().Be(command.CorrelationId);
        response.Role.Name.Should().Be(role.Name);
    }

    [Fact]
    public async Task Consume_should_publish_role_error_event_if_role_exists()
    {
        // Arrange
        await using var tester = new MassTransitTester(_configureServices);
        await using var scope = tester.Services.CreateAsyncScope();
        await scope.ServiceProvider.SeedUsersAndRoles(CancellationToken.None);
        await scope.ServiceProvider.SeedTestRole();
        var role = new Role { Name = TestExtensions.TestRoleName };
        var command = new CreateRole(role);

        // Act
        var response = await tester.TestCommand<CreateRole, CreateRoleConsumer, RoleErrorEvent>(command);

        // Assert
        response.ErrorInfo.Should().NotBeNull();
        response.ErrorInfo.ErrorCode.Should().Be(RoleErrorEvent.CreateFailedAlreadyExists);
        response.Role.Name.Should().Be(role.Name);
    }
}
