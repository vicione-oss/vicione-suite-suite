using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;

namespace Blazor.Shared.Profile.ControlPanels.ExternalIdProviders;

public partial class AddExternalIdProviderButton(
    IJSRuntime jsRuntime,
    ILogger<AddExternalIdProviderButton> logger) : ComponentBase
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
