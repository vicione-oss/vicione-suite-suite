using Sdk.Client.ControlPanels.Models;
using Sdk.Client.Interfaces;

namespace Blazor.Shared.Settings.Models;

internal interface IControlPanelEdit : IHasChangeableProperties, IAsyncDisposable
{
    bool IsRunning { get; }

    SharedResetGroup? SharedResetGroup { get; }

    Task<bool> Reset();
    void Begin();
    Task<ISaveResult> Save();
    Task<bool> Cancel();
}
