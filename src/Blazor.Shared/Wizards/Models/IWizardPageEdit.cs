using Sdk.Client.Interfaces;
using Sdk.Client.Wizards.Models;

namespace Blazor.Shared.Wizards.Models;

internal interface IWizardPageEdit : IHasChangeableProperties, IAsyncDisposable
{
    bool IsRunning { get; }

    Task<bool> Reset();
    void Begin();
    Task<ISaveResult> Save();
    Task<bool> Cancel();
}
