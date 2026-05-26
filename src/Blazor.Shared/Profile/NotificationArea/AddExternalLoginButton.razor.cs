using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;

namespace Blazor.Shared.Profile.NotificationArea;

public partial class AddExternalLoginButton(
    IJSRuntime jsRuntime,
    ILogger<AddExternalLoginButton> logger) : ComponentBase
{
    private ElementReference _form;

    private async Task OnExternalLoginSubmitAsync()
    {
        try
        {
            await jsRuntime.InvokeVoidAsync("ViciOne.Interop.submitExistingForm", _form);
        }
        catch (Exception e)
        {
            logger.LogError(e, nameof(OnExternalLoginSubmitAsync));
        }
    }
}

