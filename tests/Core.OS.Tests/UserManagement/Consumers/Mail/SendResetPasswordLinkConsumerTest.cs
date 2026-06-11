using System.IO.Abstractions;
using Core.OS.Modules;
using Core.OS.Modules.Services;
using Core.OS.UserManagement.Configuration;
using Core.OS.UserManagement.Consumers.Mail;
using Core.OS.UserManagement.Extensions;
using Core.OS.UserManagement.Templates;
using Core.Shared.Mail;
using Core.Shared.Security;
using Core.Shared.UserManagement.Contracts;
using MassTransit;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using NSubstitute;
using Sdk.Testing.Backend;
using Xunit;

namespace Core.OS.Tests.UserManagement.Consumers.Mail;

public class SendResetPasswordLinkConsumerTest
{
    private readonly Action<IBusRegistrationConfigurator> _configureServices;

    public SendResetPasswordLinkConsumerTest()
    {
        _configureServices = cfg =>
        {
            cfg.AddConsumer<SendResetPasswordLinkConsumer>();
            cfg.AddUserDbContextsInMemory();
            cfg.AddSingleton(Substitute.For<IModuleHost>());
            cfg.AddSingleton(Options.Create(new UserManagementOptions { SeedTestUsers = true }));
            cfg.AddUserManagement();
            cfg.AddSingleton(Substitute.For<IMailSender>());
            cfg.AddSingleton<FluidTemplateRenderer>();
            cfg.AddSingleton<IFileSystem>(new FileSystem());
            cfg.AddTransient<UserManagementTemplates>();
        };
    }

    [Fact]
    public async Task Should_send_email_with_the_provided_link()
    {
        // Arrange
        await using var tester = new MassTransitTester(_configureServices);
        await using var scope = tester.Services.CreateAsyncScope();
        await scope.ServiceProvider.SeedUsersAndRoles(CancellationToken.None);
        await scope.ServiceProvider.SeedTestRole();
        var suiteUser = scope.ServiceProvider.GetRequiredService<UserManager<SuiteUser>>().Users.First();
        const string callbackLink = "http://localhost:5000";
        var command = new SendResetPasswordLink(suiteUser.Id, callbackLink);

        // Act
        await tester.TestCommand<SendResetPasswordLink, SendResetPasswordLinkConsumer>(command);

        // Assert
        await scope.ServiceProvider.GetRequiredService<IMailSender>()
            .AssertSentEmailContainsCallbackLink(callbackLink);
    }

    [Fact]
    public async Task Should_fault_when_user_not_found()
    {
        // Arrange
        await using var tester = new MassTransitTester(_configureServices);
        await using var scope = tester.Services.CreateAsyncScope();
        await scope.ServiceProvider.SeedUsersAndRoles(TestContext.Current.CancellationToken);
        await scope.ServiceProvider.SeedTestRole();

        // Act & Assert — ADR-002: exceptions propagate; consumer throws InvalidOperationException for unknown user.
        var command = new SendResetPasswordLink("some non existent id", "callbackLink");
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => tester.TestCommandFault<SendResetPasswordLink, SendResetPasswordLinkConsumer>(command));
    }
}
