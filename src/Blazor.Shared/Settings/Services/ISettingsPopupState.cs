using Sdk.Client.Interfaces;

namespace Blazor.Shared.Settings.Services;

internal interface ISettingsPopupState : IHasChangeableProperties
{
    bool Visible { get; set; }
}
