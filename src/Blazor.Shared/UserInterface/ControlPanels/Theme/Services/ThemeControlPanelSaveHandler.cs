using Core.Shared.Instance.Contracts;
using Core.Shared.Instance.Services;
using Microsoft.Extensions.Logging;
using Sdk.Client.ControlPanels.Models;
using Sdk.Client.ControlPanels.Services;

namespace Blazor.Shared.UserInterface.ControlPanels.Theme.Services;

internal sealed partial class ThemeControlPanelSaveHandler(ILoginDesignService loginDesignService, ILogger<ThemeControlPanelSaveHandler> logger)
    : IControlPanelSaveHandler<ThemeControlPanelState>
{
    public Task<ISaveResult> Save(ThemeControlPanelState state, CancellationToken cancellationToken)
    {
        // The selected design is not persisted while the theme control panel stays unregistered.
        LogLoginSchemeChanged(logger, state.SelectedLoginDesign, loginDesignService.Design);

        return Task.FromResult<ISaveResult>(new SaveSuccessResult());
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Login scheme changed to {Scheme}. Provider design: {Design}")]
    private static partial void LogLoginSchemeChanged(ILogger logger, LoginDesign scheme, LoginDesign design);
}
