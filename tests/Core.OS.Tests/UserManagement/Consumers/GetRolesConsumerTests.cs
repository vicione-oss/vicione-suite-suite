using Core.OS.Modules;
using Core.OS.UserManagement.Consumers;
using Core.OS.UserManagement.Extensions;
using Core.Shared.UserManagement;
using MassTransit;
using Microsoft.Extensions.DependencyInjection;
using Sdk.Authorization;
using Sdk.Testing.Backend;
using Sdk.UserManagement.Requests;

namespace Core.OS.Tests.UserManagement.Consumers;

public class GetRolesConsumerTests
{
    private readonly Action<IBusRegistrationConfigurator> _configureServices;

    public GetRolesConsumerTests()
        => _configureServices = cfg =>
        {
            cfg.AddConsumer<GetRolesConsumer>();
            cfg.AddUserDbContextsInMemory();
            cfg.AddSingleton(Substitute.For<IModuleHost>());
            cfg.AddUserManagement();
        };

    [Fact]
    public async Task Should_be_consumed()
    {
        // Arrange
        await using var tester = new MassTransitTester(_configureServices);
        await using var scope = tester.Services.CreateAsyncScope();
        await scope.ServiceProvider.SeedUsersAndRoles(CancellationToken.None);
        var request = new GetRoles();

        // Act/Assert
        await tester.TestRequest<GetRolesResponse, GetRoles>(request);
    }

    [Fact]
    public async Task Should_return_correct_get_roles_response_with_all_roles()
    {
        // Arrange
        await using var tester = new MassTransitTester(_configureServices);
        await using var scope = tester.Services.CreateAsyncScope();
        await scope.ServiceProvider.SeedUsersAndRoles(CancellationToken.None);
        var request = new GetRoles();

        // Act
        var response = await tester.TestRequest<GetRolesResponse, GetRoles>(request);

        // Assert
        response.Roles.Should().HaveCount(1);
        response.Roles.Single().Name.Should().Be(AuthorizationConstants.AdminRoleName);
    }
}
