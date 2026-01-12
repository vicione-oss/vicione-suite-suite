using Blazor.Shared.Wizard.Services;
using Core.Shared.UserManagement.Configuration;

namespace Blazor.Shared.Onboarding.Services;

public sealed class PasswordWizardPageState(IAdministratorNameProvider administratorNameProvider)
    : WizardPageState
{
    private readonly string _userName = administratorNameProvider.GetAdministratorName();

    internal int PasswordMinimumLength => 12;

    internal bool RulesAndGuidelinesExpanded { get; set; }

    internal string Username => _userName;
    internal string Password { get; set; } = string.Empty;
    internal string RepeatPassword { get; set; } = string.Empty;
}
