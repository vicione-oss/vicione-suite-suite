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

public class SendVerifyEmailAddressLinkConsumerTest
{
    private readonly Action<IBusRegistrationConfigurator> _configureServices;

    public SendVerifyEmailAddressLinkConsumerTest()
    {
        _configureServices = cfg =>
        {
            cfg.AddConsumer<SendVerifyEmailAddressLinkConsumer>();
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
        var command = new SendVerifyEmailAddressLink(suiteUser.Id, callbackLink);

        // Act
        await tester.TestCommand<SendVerifyEmailAddressLink, SendVerifyEmailAddressLinkConsumer>(command);

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
        await scope.ServiceProvider.SeedUsersAndRoles(CancellationToken.None);
        await scope.ServiceProvider.SeedTestRole();

        // Act & Assert — ADR-002: exceptions propagate; consumer throws InvalidOperationException for unknown user.
        var command = new SendVerifyEmailAddressLink("some non existent id", "callbackLink");
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => tester.TestCommandFault<SendVerifyEmailAddressLink, SendVerifyEmailAddressLinkConsumer>(command));
    }
}
