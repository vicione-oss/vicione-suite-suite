namespace Core.Shared.Instance.Contracts;

public interface IOnboardingState
{
    Guid InstanceId { get; }
    bool Completed { get; set; }
    bool ShowWizardWhenNotCompleted { get; set; }
}
