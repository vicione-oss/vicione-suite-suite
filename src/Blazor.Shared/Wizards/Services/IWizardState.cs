using Blazor.Shared.Wizards.Models;
using Sdk.Client.Interfaces;
using Sdk.Client.Wizards.Models;

namespace Blazor.Shared.Wizards.Services;

internal interface IWizardState : IHasChangeableProperties, IHasUpdateLock
{
    /// <summary>
    /// <see langword="true"/> when a loading overlay should be displayed, otherwise <see langword="false"/>
    /// </summary>
    bool LoadingOverlayVisible { get; set; }

    /// <summary>
    /// List of steps the wizard navigates through
    /// </summary>
    IReadOnlyList<WizardStep> Steps { get; set; }

    /// <summary>
    /// Currently active step
    /// </summary>
    WizardStep? ActiveStep { get; set; }

    /// <summary>
    /// <see langword="true"/> when back button should be generally enabled, otherwise <see langword="false"/>
    /// </summary>
    bool BackButtonEnabled { get; set; }

    /// <summary>
    /// <see langword="true"/> when next button should be generally enabled, otherwise <see langword="false"/>
    /// </summary>
    bool NextButtonEnabled { get; set; }

    /// <summary>
    /// <see langword="true"/> when finish button should be generally enabled, otherwise <see langword="false"/>
    /// </summary>
    bool FinishButtonEnabled { get; set; }

    /// <summary>
    /// Error result of the last save operations
    /// </summary>
    List<SaveErrorResult> LastSaveErrorResults { get; set; }
}
