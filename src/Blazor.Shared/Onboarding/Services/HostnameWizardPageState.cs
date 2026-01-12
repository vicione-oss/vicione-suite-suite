using Sdk.Client.Wizards.Services;

namespace Blazor.Shared.Onboarding.Services;

public sealed class HostnameWizardPageState : WizardPageState
{
    internal int HostnameMaximumLength => 63;

    internal bool RulesAndGuidelinesExpanded { get; set; } = true;

    internal string? Hostname { get; set; }
}
