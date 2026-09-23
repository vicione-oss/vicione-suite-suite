using Blazor.Shared.Wizards.Models;
using Sdk.Client.Interfaces;
using Sdk.Client.Wizards.Models;

namespace Blazor.Shared.Wizards.Services;

internal interface IWizardState : IHasChangeableProperties, IHasUpdateLock
{
    bool LoadingOverlayVisible { get; set; }

    IReadOnlyList<WizardStep> Steps { get; set; }

    WizardStep? ActiveStep { get; set; }

    /// <summary>
    /// Enables the back button across the whole wizard, independently of the active step.
    /// </summary>
    bool BackButtonEnabled { get; set; }

    /// <summary>
    /// Enables the next button across the whole wizard, independently of the active step.
    /// </summary>
    bool NextButtonEnabled { get; set; }

    /// <summary>
    /// Enables the finish button across the whole wizard, independently of the active step.
    /// </summary>
    bool FinishButtonEnabled { get; set; }

    List<SaveErrorResult> LastSaveErrorResults { get; set; }
}
