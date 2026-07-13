using AwesomeAssertions;
using Core.OS.UserManagement.Security;
using Core.Shared.Mail;
using Core.Shared.Security;
using Core.Shared.UserManagement.Contracts;
using Microsoft.AspNetCore.Identity;
using NSubstitute;
using Xunit;

namespace Core.OS.Tests.UserManagement.Security;

public class AccountVerificationTest
{
    private const string UserId = "123";

    [Theory]
    [InlineData(false, true, true, false)]
    [InlineData(true, true, true, false)]
    [InlineData(true, false, true, false)]
    [InlineData(true, false, false, false)]
    [InlineData(true, true, false, true)]
    public async Task Should_signal_required_verification_only_if_mailing_works_and_settings_are_accordingly(
        bool mailSystemIsConfigured,
        bool settingsRequireVerification,
        bool userIsConfirmed,
        bool userNeedsVerification)
    {
        // Arrange
        var accountVerification
            = CreateAccountVerification(mailSystemIsConfigured, settingsRequireVerification, userIsConfirmed);

        // Act
        var needsVerification = await accountVerification.NeedsVerification(UserId, TestContext.Current.CancellationToken);

        // Assert
        needsVerification.Should().Be(userNeedsVerification);
    }

    private static AccountVerification CreateAccountVerification(bool mailSystemIsConfigured,
        bool settingsRequireVerification,
        bool userIsConfirmed)
    {
        MockDependencies(out var mailSenderStatus,
            out _,
            out var securitySettings,
            out var userManager);
        mailSenderStatus.IsConfigured().Returns(mailSystemIsConfigured);
        securitySettings.RequireAccountVerification.Returns(settingsRequireVerification);
        var suiteUser = new SuiteUser { EmailConfirmed = userIsConfirmed };
        userManager.FindByIdAsync(UserId).Returns(suiteUser);
        userManager.IsEmailConfirmedAsync(suiteUser).Returns(userIsConfirmed);
        return new AccountVerification(mailSenderStatus,
            securitySettings,
            userManager);
    }

    private static void MockDependencies(out IMailSenderStatus mailSenderStatus,
        out IMailSender mailSender,
        out ISecuritySettings securitySettings,
        out UserManager<SuiteUser> userManager)
    {
        mailSenderStatus = Substitute.For<IMailSenderStatus>();
        mailSender = Substitute.For<IMailSender>();

        securitySettings = Substitute.For<ISecuritySettings>();

        userManager = Substitute.For<UserManager<SuiteUser>>(
            Substitute.For<IUserStore<SuiteUser>>(),
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null);
    }
}
