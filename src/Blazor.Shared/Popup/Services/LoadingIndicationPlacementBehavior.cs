using System.Timers;
using Blazor.Shared.Popup.Enums;

namespace Blazor.Shared.Popup.Services;

internal sealed class LoadingIndicationPlacementBehavior : ILoadingIndicationPlacementBehavior
{
    private readonly SemaphoreSlim _semaphore = new(1);
    private bool _disposed;

    private LoadingIndicationPlacement _placement;
    private int _placementTransitionIntervalMs;

    private readonly System.Timers.Timer _loadingOverlayTimer = new()
    {
        AutoReset = false,
        Enabled = false
    };

    public LoadingIndicationPlacement Placement => _placement;
    public int DefaultPlacementTransitionIntervalMs => 2000;

    public Action? PlacementChanged { get; set; }

    public LoadingIndicationPlacementBehavior()
        => _loadingOverlayTimer.Elapsed += OnLoadingOverlayTimerElapsed;

    public void Dispose()
    {
        _disposed = true;

        _loadingOverlayTimer.Elapsed -= OnLoadingOverlayTimerElapsed;

        _loadingOverlayTimer.Stop();
        _loadingOverlayTimer.Dispose();

        _semaphore.Dispose();
    }

    public async Task Attach(CancellationToken cancellationToken = default)
    {
        try
        {
            await _semaphore.WaitAsync(cancellationToken);

            try
            {
                if (_disposed)
                    return;

                if (_loadingOverlayTimer.Enabled)
                    return;

                _placementTransitionIntervalMs = DefaultPlacementTransitionIntervalMs;

                SetPlacement(LoadingIndicationPlacement.ContentActionButtons);

                _loadingOverlayTimer.Interval = _placementTransitionIntervalMs;
                _loadingOverlayTimer.Start();
            }
            finally
            {
                _semaphore.Release();
            }
        }
        catch (OperationCanceledException)
        {
            // Nothing to do here, we return gracefully
        }
        catch (ObjectDisposedException)
        {
            // Semaphore or other object already disposed, nothing we can do, return gracefully
        }
    }

    public async Task Remove(CancellationToken cancellationToken = default)
    {
        try
        {
            await _semaphore.WaitAsync(cancellationToken);

            try
            {
                if (_disposed)
                    return;

                _loadingOverlayTimer.Stop();

                SetPlacement(LoadingIndicationPlacement.None);
            }
            finally
            {
                _semaphore.Release();
            }
        }
        catch (OperationCanceledException)
        {
            // Nothing to do here, we return gracefully
        }
        catch (ObjectDisposedException)
        {
            // Semaphore or other object already disposed, nothing we can do, return gracefully
        }
    }

    public async Task Adjust(int placementTransitionIntervalMs, CancellationToken cancellationToken = default)
    {
        try
        {
            await _semaphore.WaitAsync(cancellationToken);

            try
            {
                if (_disposed)
                    return;

                if (!_loadingOverlayTimer.Enabled)
                    return;

                // The following handling is not exactly correct because time passed since Attach() should be tracked and
                // used here to assign an accurate placement transition interval
                _loadingOverlayTimer.Stop();

                _loadingOverlayTimer.Interval = placementTransitionIntervalMs;
                _loadingOverlayTimer.Start();
            }
            finally
            {
                _semaphore.Release();
            }
        }
        catch (OperationCanceledException)
        {
            // Nothing to do here, we return gracefully
        }
        catch (ObjectDisposedException)
        {
            // Semaphore or other object already disposed, nothing we can do, return gracefully
        }
    }

    private async void OnLoadingOverlayTimerElapsed(object? _1, ElapsedEventArgs _2)
    {
        try
        {
            var success = await _semaphore.WaitAsync(100);
            if (!success)
                return;

            try
            {
                if (_disposed)
                    return;

                if (_placement == LoadingIndicationPlacement.ContentActionButtons)
                {
                    _loadingOverlayTimer.Stop();

                    SetPlacement(LoadingIndicationPlacement.Overlay);
                }
                else if (_placement == LoadingIndicationPlacement.Overlay)
                {
                    _loadingOverlayTimer.Stop();

                    SetPlacement(LoadingIndicationPlacement.None);
                }
            }
            finally
            {
                _semaphore.Release();
            }
        }
        catch (ObjectDisposedException)
        {
            // Semaphore or other object already disposed, nothing we can do, return gracefully
        }
    }

    private void SetPlacement(LoadingIndicationPlacement placement)
    {
        if (_placement != placement)
        {
            _placement = placement;

            PlacementChanged?.Invoke();
        }
    }
}
