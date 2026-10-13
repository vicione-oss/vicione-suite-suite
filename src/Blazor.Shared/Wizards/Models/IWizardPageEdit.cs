using Sdk.Client.Interfaces;
using Sdk.Client.Wizards.Models;
using Sdk.Client.Wizards.Services;

namespace Blazor.Shared.Wizards.Models;

internal interface IWizardPageEdit : IHasChangeableProperties, IAsyncDisposable
{
    IWizardPageState State { get; }

    bool IsRunning { get; }

    Task<bool> Reset();
    void Begin();
    Task<ISaveResult> Save();
    Task<bool> Cancel();
}
