using AwesomeAssertions;
using Blazor.Shared.UserManagement.ControlPanels.User.Services;
using Blazor.Shared.UserManagement.Contracts;
using Blazor.Shared.UserManagement.Services;
using Blazor.Shared.UserManagement.Services.Validators;
using Blazor.Shared.Validation.Services.Validators;
using Core.Shared.UserManagement.Contracts;
using NSubstitute;
using Sdk.Client.ControlPanels.Models;
using Xunit;

namespace Blazor.Shared.Tests.UserManagement.ControlPanels.User.Services;

public sealed class UserControlPanelSaveHandlerTests
{
    private readonly IUserService _userService = Substitute.For<IUserService>();

    private UserControlPanelSaveHandler CreateSaveHandler()
    {
        var usernameValidator = Substitute.For<IUsernameValidator>();
        usernameValidator.Validate(default!, out _).ReturnsForAnyArgs(true);

        var passwordValidator = Substitute.For<IPasswordValidator>();
        passwordValidator.Validate(default!, default!, out _).ReturnsForAnyArgs(true);

        var repeatPasswordValidator = Substitute.For<IRepeatPasswordValidator>();
        repeatPasswordValidator.Validate(default!, default!, default!, out _).ReturnsForAnyArgs(true);

        var emailValidator = Substitute.For<IEmailValidator>();
        emailValidator.Validate(default, default!, out _).ReturnsForAnyArgs(true);

        var phoneNumberValidator = Substitute.For<IPhoneNumberValidator>();
        phoneNumberValidator.Validate(default, default!, out _).ReturnsForAnyArgs(true);

        return new(_userService, usernameValidator, passwordValidator, repeatPasswordValidator, emailValidator, phoneNumberValidator);
    }

    private static UserControlPanelState CreateStateInCreateMode(string userName)
        => new() { UserProfile = new UserProfile { UserName = new UserName(userName) } };

    private static UserControlPanelState CreateStateInEditMode(string userName)
        => new() { UserName = new UserName(userName), UserProfile = new UserProfile { UserName = new UserName(userName) } };

    [Fact]
    public async Task Should_create_the_user_and_switch_to_edit_mode_on_success()
    {
        // Arrange
        var state = CreateStateInCreateMode("operator");
        _userService.CreateUser(state.UserProfile!, Arg.Any<CancellationToken>())
            .Returns(new UserManagementServiceSuccessResult());

        // Act
        var result = await CreateSaveHandler().Save(state, TestContext.Current.CancellationToken);

        // Assert
        result.Should().BeOfType<SaveSuccessResult>();
        state.UserName.Should().Be(new UserName("operator"));
        state.IsEditMode().Should().BeTrue();
        await _userService.DidNotReceive().UpdateUser(Arg.Any<UserProfile>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Should_stay_in_create_mode_when_creating_the_user_fails()
    {
        // Arrange
        var state = CreateStateInCreateMode("admin");
        _userService.CreateUser(state.UserProfile!, Arg.Any<CancellationToken>())
            .Returns(new UserManagementServiceErrorResult("User already exists"));

        // Act
        var result = await CreateSaveHandler().Save(state, TestContext.Current.CancellationToken);

        // Assert
        result.Should().BeOfType<SaveErrorResult>()
            .Which.Message.Should().Be("User already exists");
        state.UserName.Should().BeNull();
        state.IsEditMode().Should().BeFalse();
    }

    [Fact]
    public async Task Should_update_the_user_when_in_edit_mode()
    {
        // Arrange
        var state = CreateStateInEditMode("operator");
        _userService.UpdateUser(state.UserProfile!, Arg.Any<CancellationToken>())
            .Returns(new UserManagementServiceSuccessResult());

        // Act
        var result = await CreateSaveHandler().Save(state, TestContext.Current.CancellationToken);

        // Assert
        result.Should().BeOfType<SaveSuccessResult>();
        state.UserName.Should().Be(new UserName("operator"));
        await _userService.DidNotReceive().CreateUser(Arg.Any<UserProfile>(), Arg.Any<CancellationToken>());
    }
}
