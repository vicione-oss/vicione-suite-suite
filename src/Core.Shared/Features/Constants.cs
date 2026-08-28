namespace Core.Shared.Features;

public static class Constants
{
    public const string PasskeyFeatureName = "Passkeys";

    /// <summary>
    /// The wizard covers the whole UI of an instance, and only the wizard itself can end
    /// that state. An instance that does not want it — an unattended deployment, a test — turns
    /// the feature off instead.
    /// </summary>
    public const string FirstRunWizardFeatureName = "FirstRunWizard";
}
