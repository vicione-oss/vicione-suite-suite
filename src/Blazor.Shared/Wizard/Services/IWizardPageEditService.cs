using Blazor.Shared.Wizard.Models;

namespace Blazor.Shared.Wizard.Services;

internal interface IWizardPageEditService
{
    /// <summary>
    /// <see langword="true"/> when edit is ongoing, otherwise <see langword="false"/>
    /// </summary>
    bool IsDirty { get; }

    event Func<Task<ISaveResult>>? OnSave;
    event Func<Task>? OnCancel;
    event Func<Task>? OnBeginEdit;
    event Func<Task>? OnCancelEdit;

    Task BeginEdit();
    Task CancelEdit();
    Task<WizardPageEditFinishResult> FinishEdit();

    /// <summary>
    /// Reset internal states to default values like <see cref="IsDirty"/>
    /// </summary>
    void Reset();
}

internal interface IWizardPageEditService<TContext> : IWizardPageEditService;
