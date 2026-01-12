using System.Security.Claims;
using Sdk.Client.ControlPanels.Services;

namespace Blazor.Shared.Settings.Services;

/// <summary>
/// Cache for <see cref="IControlPanelPageRegistryItem"/> instances representing control panels accessible to the current user
/// </summary>
public interface IControlPanelRegistryItemCache
{
    event Action? Changed;

    Task<IEnumerable<IControlPanelRegistryItem>> GetAll(ClaimsPrincipal? user, CancellationToken cancellationToken = default);
}
