namespace Blazor.Shared.Wizard.Services;

public interface IWizardPageDescriptor
{
    /// <summary>
    /// Text used to categorize the settings provided by the control panel
    /// </summary>
    string Title { get; }

    /// <summary>
    /// Optional position in the list of all pages available in the wizard
    /// </summary>
    /// <remarks>
    /// When Position X of page A is lower than Position Y of page B then page A or an aspect of page A is rendered first.
    /// 
    /// In context of the navigation order this means the user is guided to page A first, then page B.
    /// 
    /// If not set then the page is rendered after all pages having a position in registration order.
    /// </remarks>
    int? Position => null;
}
