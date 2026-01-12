using Blazor.Shared.Settings.Models;
using Sdk.Client.Services;

namespace Blazor.Shared.Settings.Services;

internal interface IControlPanelEditRegistry : IRegistry<IControlPanelEdit>
{
    void Add(IControlPanelEdit item);
}
