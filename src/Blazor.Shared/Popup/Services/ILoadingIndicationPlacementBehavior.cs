using Blazor.Shared.Popup.Enums;

namespace Blazor.Shared.Popup.Services;

internal interface ILoadingIndicationPlacementBehavior : IDisposable
{
    LoadingIndicationPlacement Placement { get; }
    int DefaultPlacementTransitionIntervalMs { get; }

    Action? PlacementChanged { get; set; }

    Task Attach(CancellationToken cancellationToken = default);
    Task Remove(CancellationToken cancellationToken = default);

    Task Adjust(int placementTransitionIntervalMs, CancellationToken cancellationToken = default);
}
