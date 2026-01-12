using Blazor.Shared.Onboarding.Models;
using Blazor.Shared.UserManagement.Services.Validators;
using Blazor.Shared.Wizard.Models;
using Blazor.Shared.Wizard.Services;
using Core.Shared.UserManagement.Configuration;
using Core.Shared.UserManagement.Contracts;
using Microsoft.AspNetCore.Identity;
using ViciOne.Ui.Localization.Resources;

namespace Blazor.Shared.Onboarding.Services;

internal sealed class PasswordWizardPageSaveHandler(IUsernameValidator usernameValidator, IPasswordValidator passwordValidator,
    IRepeatPasswordValidator repeatPasswordValidator, UserManager<SuiteUser> userManager,
    IAdministratorInitialPasswordProvider administratorInitialPasswordProvider, ITargetConfigurationProvider targetConfigurationProvider)
        : IWizardPageSaveHandler<PasswordWizardPageState>
{
    public async Task<ISaveResult> Save(PasswordWizardPageState state, CancellationToken cancellationToken)
    {
        var culture = Localization.ValidationMessages.Culture;

        if (!usernameValidator.Validate(state.Username, out var errorMessage))
            return new SaveErrorResult(errorMessage);

        var field = CommonVocabulary.Password;
        var currentPassword = administratorInitialPasswordProvider.GetAdministratorInitialPassword();

        if (string.CompareOrdinal(state.Password, currentPassword) == 0)
            return new SaveErrorResult(string.Format(culture, Localization.PasswordWizardPageSaveHandler.FieldMustNotBeEqualToCurrentPassword, field));

        if (!passwordValidator.Validate(state.Password, field, out errorMessage))
            return new SaveErrorResult(errorMessage);

        if (!repeatPasswordValidator.Validate(state.RepeatPassword, Onboarding.Localization.SettingsFieldLabels.RepeatPassword, state.Password, out errorMessage))
            return new SaveErrorResult(errorMessage);

        var user = await userManager.FindByNameAsync(state.Username);
        if (user is null)
            return new SaveErrorResult(string.Format(culture, Localization.ValidationMessages.UserWasNotFound, state.Username));

        foreach (var identityPasswordValidator in userManager.PasswordValidators)
        {
            var identityResult = await identityPasswordValidator.ValidateAsync(userManager, user, state.Password);
            if (!identityResult.Succeeded)
                return new SaveErrorResult(string.Join(". ", identityResult.Errors.Select(identityError => identityError.Description)));
        }

        var targetConfiguration = await targetConfigurationProvider.GetTargetConfiguration(cancellationToken);
        targetConfiguration.UserCredentials = new UserCredentials { UserName = state.Username, Password = state.Password };

        return new SaveSuccessResult();
    }
}
