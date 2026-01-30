using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;
using Sdk.Client.Extensions;
using Sdk.Client.Services;
using ViciOne.Ui.MonochromeIcons.Core.Enums;

namespace Blazor.Shared.Components.CopyToClipboardButton;

public sealed partial class CopyToClipboardButton : ComponentBase, IAsyncDisposable
{
    private IJSObjectReference? _jsModule;

    [Parameter, EditorRequired] public Func<Task<string>> SetText { get; set; }

    [Parameter] public MonochromeIconSize IconSize { get; set; } = MonochromeIconSize.Small;

    [Inject] private IJsInterop JsInterop { get; set; } = default!;

    [Inject] private ILogger<CopyToClipboardButton> Logger { get; set; } = default!;

    private async Task ButtonClick()
    {
        _jsModule ??= await JsInterop.IncludeModuleScript<SharedClientModule>("copy-to-clipboard-button.js");
        var text = await SetText();
        await _jsModule!.InvokeVoidAsync("copyTextToClipboard", text);
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync() => await _jsModule.TryDisposeAsync(Logger);
}

