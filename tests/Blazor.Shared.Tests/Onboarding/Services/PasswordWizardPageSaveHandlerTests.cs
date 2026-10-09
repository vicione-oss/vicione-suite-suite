using Blazor.Shared.Onboarding.Models;
using Blazor.Shared.Onboarding.Services;
using Blazor.Shared.UserManagement.Services.Validators;
using Core.Shared.UserManagement.Configuration;
using Core.Shared.UserManagement.Contracts;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Sdk.Client.Wizards.Models;
using IIdentityPasswordValidator = Microsoft.AspNetCore.Identity.IPasswordValidator<Core.Shared.UserManagement.Contracts.SuiteUser>;
using IPasswordValidator = Blazor.Shared.UserManagement.Services.Validators.IPasswordValidator;

namespace Blazor.Shared.Tests.Onboarding.Services;

public sealed class PasswordWizardPageSaveHandlerTests
{
    private const string AdministratorName = "admin";
    private const string InitialPassword = "initial-password";
    private const string NewPassword = "new-password-123";

    private readonly IUsernameValidator _usernameValidator = Substitute.For<IUsernameValidator>();
    private readonly IPasswordValidator _passwordValidator = Substitute.For<IPasswordValidator>();
    private readonly IRepeatPasswordValidator _repeatPasswordValidator = Substitute.For<IRepeatPasswordValidator>();
    private readonly IAdministratorInitialPasswordProvider _initialPasswordProvider = Substitute.For<IAdministratorInitialPasswordProvider>();
    private readonly IAdministratorNameProvider _administratorNameProvider = Substitute.For<IAdministratorNameProvider>();
    private readonly ITargetConfigurationProvider _targetConfigurationProvider = Substitute.For<ITargetConfigurationProvider>();
    private readonly IIdentityPasswordValidator _identityPasswordValidator = Substitute.For<IIdentityPasswordValidator>();
    private readonly TargetConfiguration _targetConfiguration = new();
    private readonly UserManager<SuiteUser> _userManager;

    public PasswordWizardPageSaveHandlerTests()
    {
        _usernameValidator
            .Validate(Arg.Any<string>(), out Arg.Any<string?>())
            .Returns(true);

        _passwordValidator
            .Validate(Arg.Any<string>(), Arg.Any<string>(), out Arg.Any<string?>())
            .Returns(true);

        _repeatPasswordValidator
            .Validate(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), out Arg.Any<string?>())
            .Returns(true);

        _initialPasswordProvider.GetAdministratorInitialPassword().Returns(InitialPassword);
        _administratorNameProvider.GetAdministratorName().Returns(AdministratorName);

        _targetConfigurationProvider
            .GetTargetConfiguration(Arg.Any<CancellationToken>())
            .Returns(_targetConfiguration);

        _identityPasswordValidator
            .ValidateAsync(Arg.Any<UserManager<SuiteUser>>(), Arg.Any<SuiteUser>(), Arg.Any<string?>())
            .Returns(IdentityResult.Success);

        _userManager = Substitute.For<UserManager<SuiteUser>>(
            Substitute.For<IUserStore<SuiteUser>>(),
            Substitute.For<IOptions<IdentityOptions>>(),
            Substitute.For<IPasswordHasher<SuiteUser>>(),
            Substitute.For<IEnumerable<IUserValidator<SuiteUser>>>(),
            new[] { _identityPasswordValidator },
            Substitute.For<ILookupNormalizer>(),
            Substitute.For<IdentityErrorDescriber>(),
            Substitute.For<IServiceProvider>(),
            Substitute.For<ILogger<UserManager<SuiteUser>>>());

        _userManager.FindByNameAsync(AdministratorName).Returns(new SuiteUser { UserName = AdministratorName });
    }

    private PasswordWizardPageSaveHandler CreateHandler() =>
        new(_usernameValidator, _passwordValidator, _repeatPasswordValidator, _userManager, _initialPasswordProvider,
            _targetConfigurationProvider);

    private PasswordWizardPageState CreateState(string password = NewPassword, string? repeatPassword = null) =>
        new(_administratorNameProvider) { Password = password, RepeatPassword = repeatPassword ?? password };

    [Fact]
    public async Task Should_store_credentials_in_target_configuration()
    {
        // Arrange
        var handler = CreateHandler();

        // Act
        var result = await handler.Save(CreateState(), TestContext.Current.CancellationToken);

        // Assert
        result.Should().BeOfType<SaveSuccessResult>();
        _targetConfiguration.UserCredentials.Should().Be(new UserCredentials(AdministratorName, NewPassword));
    }

    [Fact]
    public async Task Should_reject_invalid_username()
    {
        // Arrange
        _usernameValidator
            .Validate(Arg.Any<string>(), out Arg.Any<string?>())
            .Returns(x =>
            {
                x[1] = "invalid username";
                return false;
            });

        var handler = CreateHandler();

        // Act
        var result = await handler.Save(CreateState(), TestContext.Current.CancellationToken);

        // Assert
        result.Should().BeOfType<SaveErrorResult>()
            .Which.Message.Should().Be("invalid username");
        _targetConfiguration.UserCredentials.Should().BeNull();
    }

    [Fact]
    public async Task Should_reject_initial_password()
    {
        // Arrange
        var handler = CreateHandler();

        // Act
        var result = await handler.Save(CreateState(InitialPassword), TestContext.Current.CancellationToken);

        // Assert
        result.Should().BeOfType<SaveErrorResult>();
        _passwordValidator.DidNotReceive().Validate(Arg.Any<string>(), Arg.Any<string>(), out Arg.Any<string?>());
        _targetConfiguration.UserCredentials.Should().BeNull();
    }

    [Fact]
    public async Task Should_reject_password_that_fails_the_password_rules()
    {
        // Arrange
        _passwordValidator
            .Validate(Arg.Any<string>(), Arg.Any<string>(), out Arg.Any<string?>())
            .Returns(x =>
            {
                x[2] = "too short";
                return false;
            });

        var handler = CreateHandler();

        // Act
        var result = await handler.Save(CreateState(), TestContext.Current.CancellationToken);

        // Assert
        result.Should().BeOfType<SaveErrorResult>()
            .Which.Message.Should().Be("too short");
        _targetConfiguration.UserCredentials.Should().BeNull();
    }

    [Fact]
    public async Task Should_reject_mismatching_repeat_password()
    {
        // Arrange
        _repeatPasswordValidator
            .Validate(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), out Arg.Any<string?>())
            .Returns(x =>
            {
                x[3] = "passwords differ";
                return false;
            });

        var handler = CreateHandler();

        // Act
        var result = await handler.Save(CreateState(repeatPassword: "other"), TestContext.Current.CancellationToken);

        // Assert
        result.Should().BeOfType<SaveErrorResult>()
            .Which.Message.Should().Be("passwords differ");
        _targetConfiguration.UserCredentials.Should().BeNull();
    }

    [Fact]
    public async Task Should_reject_unknown_user()
    {
        // Arrange
        _userManager.FindByNameAsync(AdministratorName).Returns((SuiteUser?)null);

        var handler = CreateHandler();

        // Act
        var result = await handler.Save(CreateState(), TestContext.Current.CancellationToken);

        // Assert
        result.Should().BeOfType<SaveErrorResult>()
            .Which.Message.Should().Contain(AdministratorName);
        _targetConfiguration.UserCredentials.Should().BeNull();
    }

    [Fact]
    public async Task Should_return_all_identity_password_validator_errors()
    {
        // Arrange
        _identityPasswordValidator
            .ValidateAsync(Arg.Any<UserManager<SuiteUser>>(), Arg.Any<SuiteUser>(), Arg.Any<string?>())
            .Returns(IdentityResult.Failed(
                new IdentityError { Description = "Needs a digit" },
                new IdentityError { Description = "Needs an uppercase letter" }));

        var handler = CreateHandler();

        // Act
        var result = await handler.Save(CreateState(), TestContext.Current.CancellationToken);

        // Assert
        result.Should().BeOfType<SaveErrorResult>()
            .Which.Message.Should().Be("Needs a digit. Needs an uppercase letter");
        _targetConfiguration.UserCredentials.Should().BeNull();
    }
}
