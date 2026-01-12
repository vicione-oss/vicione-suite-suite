namespace Blazor.Shared.Wizard.Services;

public sealed class WizardPageStateChangedEventArgs(IWizardPageState sender, IEnumerable<string> propertyNames) : EventArgs
{
    public IWizardPageState Sender => sender;
    public IEnumerable<string> PropertyNames => propertyNames;
}
