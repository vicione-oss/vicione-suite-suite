using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;
using Sdk.Client.Extensions;
using Sdk.Client.Services;

namespace Blazor.Shared.Components.Carousel;

/// <summary>
/// Shows the item at <see cref="ActiveIndex"/> and slides sideways while <see cref="SlideFrom"/> is set; the caller decides
/// whether a change slides or jumps.
/// </summary>
public sealed partial class Carousel<TItem> : ComponentBase, IAsyncDisposable
    where TItem : class
{
    private ElementReference _track;
    private IJSObjectReference? _jsModule;

    private TItem? _renderedSlideFrom;
    private bool _slideStarted;

    private bool _disposed;

    private readonly CancellationTokenSource _cancellationTokenSource = new();

    private CancellationToken CancellationToken => _cancellationTokenSource.Token;

    [Parameter, EditorRequired] public IReadOnlyList<TItem> Items { get; set; }

    /// <summary>
    /// Index in <see cref="Items"/> of the item shown, or sliding into view; a negative index shows no item.
    /// </summary>
    [Parameter, EditorRequired] public int ActiveIndex { get; set; }

    /// <summary>
    /// Item kept visible next to the active one while the track slides; <see langword="null"/> shows the active item at once.
    /// Must be one of <see cref="Items"/>.
    /// </summary>
    [Parameter] public TItem? SlideFrom { get; set; }

    [Parameter, EditorRequired] public RenderFragment<TItem> ItemTemplate { get; set; }

    /// <summary>
    /// Raised with <see cref="SlideFrom"/> when the slide has ended, also when waiting for its end failed; the caller clears
    /// <see cref="SlideFrom"/>. Not raised for a slide the caller replaced or cleared meanwhile, nor on disposal.
    /// </summary>
    [Parameter] public EventCallback<TItem> OnSlideEnded { get; set; }

    [Inject] private IJsInterop JsInterop { get; set; } = default!;

    [Inject] private ILogger<Carousel<TItem>> Logger { get; set; } = default!;

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.CompareExchange(ref _disposed, true, false))
            return;

        await _cancellationTokenSource.CancelAsync();
        _cancellationTokenSource.Dispose();

        await _jsModule.TryDisposeAsync(Logger);
    }

    protected override void OnParametersSet()
    {
        // Compared with the last value instead of checking for null, so a slide from the same item as the previous one,
        // e.g. next, back and next again, is still awaited.
        if (SlideFrom == _renderedSlideFrom)
            return;

        _renderedSlideFrom = SlideFrom;
        _slideStarted = SlideFrom is not null;
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        await base.OnAfterRenderAsync(firstRender);

        if (_disposed || !_slideStarted || _renderedSlideFrom is not { } slideFrom)
            return;

        _slideStarted = false;

        try
        {
            try
            {
                _jsModule ??= await JsInterop.IncludeModuleScript<SharedClientModule>("carousel.js", CancellationToken);

                // The import ignores cancellation, so a carousel disposed meanwhile has not released the module yet.
                if (_disposed)
                {
                    await _jsModule.TryDisposeAsync(Logger);

                    return;
                }

                if (_jsModule is not null)
                    await _jsModule.InvokeVoidAsync("waitForTransitionEnd", CancellationToken, _track);
            }
            catch (JSException ex)
            {
                // A failed wait ends the slide anyway, so the item sliding out does not stay mounted off-screen.
                Logger.LogWarning(ex, "Waiting for the carousel slide to end failed.");
            }
            catch (OperationCanceledException ex) when (!_disposed)
            {
                // The JS interop call timed out; the slide ends anyway, so the item sliding out does not stay mounted off-screen.
                Logger.LogWarning(ex, "Waiting for the carousel slide to end timed out.");
            }

            if (_disposed || SlideFrom != slideFrom)
                return;

            await OnSlideEnded.InvokeAsync(slideFrom);
        }
        catch (OperationCanceledException)
        {
            // The carousel was disposed mid-slide; nothing left to clean up.
        }
        catch (ObjectDisposedException)
        {
            // The carousel was disposed and its CancellationTokenSource with it; nothing left to clean up.
        }
        catch (JSDisconnectedException)
        {
            // The circuit is gone mid-slide; nothing left to clean up.
        }
    }
}
