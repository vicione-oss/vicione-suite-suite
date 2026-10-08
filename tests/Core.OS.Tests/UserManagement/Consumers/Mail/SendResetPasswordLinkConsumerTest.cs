using System.IO.Abstractions;
using Core.OS.DbContext;
using Core.OS.Modules;
using Core.OS.Modules.Services;
using Core.OS.UserManagement.Configuration;
using Core.OS.UserManagement.Consumers.Mail;
using Core.OS.UserManagement.Extensions;
using Core.OS.UserManagement.Templates;
using Core.Shared.Instance.Contracts;
using Core.Shared.Mail;
using Core.Shared.Security;
using Core.Shared.UserManagement.Contracts;
using MassTransit;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Sdk.Testing.Backend;

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
            cfg.AddApplicationDbContextsInMemory();
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

    [Theory]
    [InlineData("de-DE", "en-US", "Passwort zurücksetzen", "Bitte setzen Sie Ihr Passwort über den folgenden Link zurück")]
    [InlineData(null, "de-DE", "Passwort zurücksetzen", "Bitte setzen Sie Ihr Passwort über den folgenden Link zurück")]
    [InlineData(null, "en-US", "Reset password", "Please reset your password using the following link")]
    public async Task Should_send_email_in_user_language_or_else_instance_default(string? userLanguage,
        string instanceCulture, string expectedSubject, string expectedBodyText)
    {
        // Arrange
        await using var tester = new MassTransitTester(_configureServices);
        await using var scope = tester.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
        dbContext.CrossInstanceConfiguration.Add(new CrossInstanceConfiguration { CultureName = instanceCulture });
        await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);
        await scope.ServiceProvider.SeedUsersAndRoles(TestContext.Current.CancellationToken);
        await scope.ServiceProvider.SeedTestRole();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<SuiteUser>>();
        var suiteUser = userManager.Users.First();
        suiteUser.Language = userLanguage;
        await userManager.UpdateAsync(suiteUser);
        var command = new SendResetPasswordLink(suiteUser.Id, "http://localhost:5000");

        // Act
        await tester.TestCommand<SendResetPasswordLink, SendResetPasswordLinkConsumer>(command);

        // Assert
        await scope.ServiceProvider.GetRequiredService<IMailSender>()
            .Received()
            .SendMail(Arg.Is<Message>(m => m.Subject == expectedSubject && m.Body!.Contains(expectedBodyText)),
                Arg.Any<CancellationToken>());
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
