using System.ComponentModel.DataAnnotations;

namespace Core.Shared.Instance.Contracts;

public sealed class OnboardingState : IOnboardingState
{
    [Key]
    public required Guid InstanceId { get; set; }

    public bool Completed { get; set; }
    public bool ShowWizardWhenNotCompleted { get; set; } = true;
}
