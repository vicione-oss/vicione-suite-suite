using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using Blazor.Shared.UserManagement.Contracts;
using Blazor.Shared.UserManagement.Services;
using Blazor.Shared.UserManagement.Services.Validators;
using Blazor.Shared.Validation.Services.Validators;
using Sdk.Client.ControlPanels.Models;
using Sdk.Client.ControlPanels.Services;
using ViciOne.Ui.Localization.Resources;
using Labels = Blazor.Shared.UserManagement.Localization.Labels;

namespace Blazor.Shared.UserManagement.ControlPanels.User.Services;

internal sealed class UserControlPanelSaveHandler(IUserService userService, IUsernameValidator usernameValidator,
    IPasswordValidator passwordValidator, IRepeatPasswordValidator repeatPasswordValidator, IEmailValidator emailValidator,
    IPhoneNumberValidator phoneNumberValidator)
        : IControlPanelSaveHandler<UserControlPanelState>
{
    public async Task<ISaveResult> Save(UserControlPanelState state, CancellationToken cancellationToken)
    {
        if (state.UserProfile is null)
            throw new InvalidOperationException("No user profile provided");

        if (!usernameValidator.Validate(state.UserProfile.UserName.Value, out var errorMessage))
            return new SaveErrorResult(errorMessage);

        if (!ValidatePasswordFields(state, out errorMessage))
            return new SaveErrorResult(errorMessage);

        if (!emailValidator.Validate(state.UserProfile.Email, CommonVocabulary.Email, out errorMessage))
            return new SaveErrorResult(errorMessage);

        if (!phoneNumberValidator.Validate(state.UserProfile.Mobile, CommonVocabulary.Cellphone, out errorMessage))
            return new SaveErrorResult(errorMessage);

        if (!phoneNumberValidator.Validate(state.UserProfile.PhoneNumber, CommonVocabulary.Phone, out errorMessage))
            return new SaveErrorResult(errorMessage);

        var culture = CultureInfo.CurrentCulture;
        var universalDateFormat = User.Localization.Constants.UniversalDateFormat;

        if (ValidatePasswordExpirationDate(state.PasswordExpirationDateString, universalDateFormat, culture, out var passwordExpirationDate))
            state.UserProfile.PasswordExpirationDate = passwordExpirationDate;
        else
            return new SaveErrorResult(string.Format(culture, Localization.UserControlPanelSaveHandler.ThePasswordExpirationDateIsNotInTheCorrectFormat, universalDateFormat));

        state.UserProfile.Language = state.SelectedCulture?.Name;
        state.UserProfile.TimeZone = state.SelectedTimeZone?.Id;

        state.UserProfile.CurrentPassword = state.CurrentPassword;
        state.UserProfile.NewPassword = state.NewPassword;

        IUserManagementServiceResult result;

        if (state.IsEditMode())
            result = await userService.UpdateUser(state.UserProfile, cancellationToken);
        else
            result = await userService.CreateUser(state.UserProfile, cancellationToken);

        if (result is UserManagementServiceSuccessResult)
        {
            state.PermissionEditContext = null;

            return new SaveSuccessResult();
        }

        if (result is UserManagementServiceErrorResult errorResult)
            return new SaveErrorResult(errorResult.ErrorMessage);

        throw new NotSupportedException("Result type unknown");
    }

    private bool ValidatePasswordFields(UserControlPanelState state, [MaybeNullWhen(true)] out string errorMessage)
    {
        if (state.IsEditMode())
        {
            if (!string.IsNullOrEmpty(state.CurrentPassword) || !string.IsNullOrEmpty(state.NewPassword) || !string.IsNullOrEmpty(state.RepeatNewPassword))
            {
                // no validation of _currentPassword, this is done by the backend whereas potential errors then arrive in Consume()

                return ValidateNewPasswordAndRepetition(state, out errorMessage);
            }
            else
            {
                errorMessage = default;

                return true;
            }
        }
        else
        {
            return ValidateNewPasswordAndRepetition(state, out errorMessage);
        }
    }

    private static bool ValidatePasswordExpirationDate(string? passwordExpirationDateString, string universalDateFormat,
        CultureInfo? culture, out DateTimeOffset? passwordExpirationDate)
    {
        if (string.IsNullOrEmpty(passwordExpirationDateString))
        {
            passwordExpirationDate = null;

            return true;
        }

        if (DateTime.TryParseExact(passwordExpirationDateString, universalDateFormat, culture, DateTimeStyles.AssumeLocal,
            out var dateTimeParse))
        {
            passwordExpirationDate = new(dateTimeParse.ToUniversalTime());

            return true;
        }
        else
        {
            passwordExpirationDate = null;

            return false;
        }
    }

    private bool ValidateNewPasswordAndRepetition(UserControlPanelState state, [MaybeNullWhen(true)] out string errorMessage)
    {
        var newPassword = state.NewPassword ?? string.Empty;

        if (!passwordValidator.Validate(newPassword, Labels.NewPassword, out errorMessage))
            return false;

        if (!repeatPasswordValidator.Validate(state.RepeatNewPassword ?? string.Empty, Labels.RepeatNewPassword, newPassword, out errorMessage))
            return false;

        errorMessage = null;

        return true;
    }
}
