using Microsoft.Extensions.Logging;
using Sdk.Client.ControlPanels.Services;

namespace Blazor.Shared.Settings.Extensions;

internal static partial class IUpdateControlPanelRegistryHandlerExtensions
{
    /// <exception cref="OperationCanceledException" />
    public static async Task Execute(this IEnumerable<IUpdateControlPanelRegistryHandler> updateControlPanelRegistryHandlers,
        ILogger logger, CancellationToken cancellationToken)
    {
        foreach (var updateControlPanelRegistryHandler in updateControlPanelRegistryHandlers)
        {
            try
            {
                await updateControlPanelRegistryHandler.Execute(cancellationToken);
            }
            catch (OperationCanceledException operationCanceledException)
            {
                UpdateControlPanelRegistryHandlerCanceled(logger, operationCanceledException, updateControlPanelRegistryHandler.GetType().Name);

                throw;
            }
            catch (Exception exception)
            {
                UpdateControlPanelRegistryHandlerFailed(logger, exception, updateControlPanelRegistryHandler.GetType().Name);
            }
        }
    }

    [LoggerMessage(1, LogLevel.Information, "{Handler} canceled")]
    private static partial void UpdateControlPanelRegistryHandlerCanceled(ILogger logger,
        Exception exception, string handler);

    [LoggerMessage(2, LogLevel.Error, "{Handler} failed")]
    private static partial void UpdateControlPanelRegistryHandlerFailed(ILogger logger,
        Exception exception, string handler);
}
