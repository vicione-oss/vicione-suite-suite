using Core.OS.UserManagement.Security;
using Core.Shared.Mail;
using Core.Shared.Security;
using Core.Shared.UserManagement.Contracts;
using Microsoft.AspNetCore.Identity;

namespace Core.OS.Tests.UserManagement.Security;

public sealed class AccountVerificationTest : IDisposable
{
    private const string UserId = "123";

    private readonly UserManager<SuiteUser> _userManager = TestFactory.CreateUserManager();

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

    private AccountVerification CreateAccountVerification(bool mailSystemIsConfigured,
        bool settingsRequireVerification,
        bool userIsConfirmed)
    {
        var mailSenderStatus = Substitute.For<IMailSenderStatus>();
        var securitySettings = Substitute.For<ISecuritySettings>();

        mailSenderStatus.IsConfigured().Returns(mailSystemIsConfigured);
        securitySettings.RequireAccountVerification.Returns(settingsRequireVerification);

        var suiteUser = new SuiteUser { EmailConfirmed = userIsConfirmed };
        _userManager.FindByIdAsync(UserId).Returns(suiteUser);
        _userManager.IsEmailConfirmedAsync(suiteUser).Returns(userIsConfirmed);

        return new AccountVerification(mailSenderStatus, securitySettings, _userManager);
    }

    public void Dispose() => _userManager.Dispose();
}
