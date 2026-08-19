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
using Sdk.UserManagement.Contracts;
using Sdk.UserManagement.Requests;

namespace Core.OS.Tests.UserManagement.Consumers;

public class GetUserInformationConsumerTests
{
    private readonly Action<IBusRegistrationConfigurator> _configureServices;

    public GetUserInformationConsumerTests()
        => _configureServices = cfg =>
        {
            cfg.AddConsumer<GetUserInformationConsumer>();
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
        var request = new GetUserInformation();

        // Act/Assert
        await tester.TestRequest<GetUserInformationResponse, GetUserInformation>(request);
    }

    [Fact]
    public async Task Should_return_correct_get_user_information_response_with_all_users()
    {
        // Arrange
        await using var tester = new MassTransitTester(_configureServices);
        await using var scope = tester.Services.CreateAsyncScope();
        await scope.ServiceProvider.SeedUsersAndRoles(CancellationToken.None);
        await scope.ServiceProvider.SeedTestRole();
        var request = new GetUserInformation();

        // Act/Assert
        var response = await tester.TestRequest<GetUserInformationResponse, GetUserInformation>(request);
        response.Users.Should().BeEquivalentTo(SeedingExtensions.Users.Select(s => s.ToUserInformation()));
    }

    [Fact]
    public async Task Should_return_correct_get_user_information_response_with_single_user()
    {
        // Arrange
        await using var tester = new MassTransitTester(_configureServices);
        await using var scope = tester.Services.CreateAsyncScope();
        await scope.ServiceProvider.SeedUsersAndRoles(CancellationToken.None);
        await scope.ServiceProvider.SeedTestRole();
        var request = new GetUserInformation(SeedingExtensions.Bob.UserName);

        // Act/Assert
        var response = await tester.TestRequest<GetUserInformationResponse, GetUserInformation>(request);
        response.Should().BeEquivalentTo(new GetUserInformationResponse([SeedingExtensions.Bob.ToUserInformation()]));
    }

    [Fact]
    public async Task Should_return_error_for_unknown_username()
    {
        // Arrange
        await using var tester = new MassTransitTester(_configureServices);
        await using var scope = tester.Services.CreateAsyncScope();
        await scope.ServiceProvider.SeedUsersAndRoles(CancellationToken.None);
        var userName = "Boring invalid user name";
        var request = new GetUserInformation(userName);

        // Act/Assert
        var response = await tester.TestRequest<GetUserInformationResponse, GetUserInformation>(request);
        response.RequestError.Should().NotBeNull();
        response.RequestError!.ErrorCode.Should().Be(UserErrorCodes.NotFound);
        response.Users.Should().BeEmpty();
    }
}
