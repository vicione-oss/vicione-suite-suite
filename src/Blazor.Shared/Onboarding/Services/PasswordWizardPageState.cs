using Core.Shared.UserManagement.Configuration;
using Sdk.Client.Wizards.Services;

namespace Blazor.Shared.Onboarding.Services;

public sealed class PasswordWizardPageState(IAdministratorNameProvider administratorNameProvider)
    : WizardPageState
{
    internal const int PasswordMinimumLength = 12;

    private readonly string _userName = administratorNameProvider.GetAdministratorName();

    internal bool RulesAndGuidelinesExpanded { get; set; }

    internal string Username => _userName;
    internal string Password { get; set; } = string.Empty;
    internal string RepeatPassword { get; set; } = string.Empty;
}
