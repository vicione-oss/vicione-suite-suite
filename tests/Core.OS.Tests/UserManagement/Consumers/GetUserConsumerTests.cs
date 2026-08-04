using Core.OS.Modules;
using Core.OS.UserManagement.Configuration;
using Core.OS.UserManagement.Consumers;
using Core.OS.UserManagement.Extensions;
using Core.Shared.UserManagement.Contracts;
using Core.Shared.UserManagement.Events;
using Core.Shared.UserManagement.Requests;
using MassTransit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Sdk.Testing.Backend;

namespace Core.OS.Tests.UserManagement.Consumers;

public class GetUserConsumerTests
{
    private readonly Action<IBusRegistrationConfigurator> _configureServices;

    public GetUserConsumerTests()
        => _configureServices = cfg =>
        {
            cfg.AddConsumer<GetUsersConsumer>();
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
        var request = new GetUsers();

        // Act/Assert
        await tester.TestRequest<GetUsersResponse, GetUsers>(request);
    }

    [Fact]
    public async Task Should_return_correct_get_users_response_with_all_users()
    {
        // Arrange
        await using var tester = new MassTransitTester(_configureServices);
        await using var scope = tester.Services.CreateAsyncScope();
        await scope.ServiceProvider.SeedUsersAndRoles(CancellationToken.None);
        await scope.ServiceProvider.SeedTestRole();
        var request = new GetUsers();

        // Act/Assert
        var response = await tester.TestRequest<GetUsersResponse, GetUsers>(request);
        response.Users.Should().BeEquivalentTo(SeedingExtensions.Users.Select(s => s.ToUserProfile()));
    }

    [Fact]
    public async Task Should_return_correct_get_users_response_with_single_user()
    {
        // Arrange
        await using var tester = new MassTransitTester(_configureServices);
        await using var scope = tester.Services.CreateAsyncScope();
        await scope.ServiceProvider.SeedUsersAndRoles(CancellationToken.None);
        await scope.ServiceProvider.SeedTestRole();
        var request = new GetUsers(new UserName(SeedingExtensions.Bob.UserName));

        // Act/Assert
        var response = await tester.TestRequest<GetUsersResponse, GetUsers>(request);
        response.Should().BeEquivalentTo(new GetUsersResponse([SeedingExtensions.Bob.ToUserProfile()]));
    }

    [Fact]
    public async Task Should_return_error_for_unknown_username()
    {
        // Arrange
        await using var tester = new MassTransitTester(_configureServices);
        await using var scope = tester.Services.CreateAsyncScope();
        await scope.ServiceProvider.SeedUsersAndRoles(CancellationToken.None);
        var userName = new UserName("Boring invalid user name");
        var request = new GetUsers(userName);

        // Act/Assert
        var response = await tester.TestRequest<GetUsersResponse, GetUsers>(request);
        response.RequestError.Should().NotBeNull();
        response.RequestError!.ErrorCode.Should().Be(UserErrorCodes.NotFound);
        response.Users.Should().BeEmpty();
    }
}
