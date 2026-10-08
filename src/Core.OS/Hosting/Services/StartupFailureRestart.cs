using System.Net.Mime;
using Core.OS.Hosting.Pages;
using Core.OS.HostManagement;
using Core.OS.HostManagement.Extensions;
using Core.OS.Instance;

namespace Core.OS.Hosting.Services;

/// <summary>
/// Restarts the Suite after an action on a startup failure page, or answers with <see cref="RestartDisabledPage"/>
/// when HostManagement disables RestartService.
/// </summary>
internal static partial class StartupFailureRestart
{
    /// <param name="pipeClient">The client that asks HostManagement for the capabilities and the restart.</param>
    /// <param name="instance">The options naming the Suite's service.</param>
    /// <param name="logger">The logger of the calling page.</param>
    /// <param name="delayMs">The time the redirect gets to reach the browser before the host goes down.</param>
    /// <param name="styleSheet">The stylesheet of the calling page, which its Content Security Policy already allows.</param>
    public static async Task<IResult> RestartIfEnabled(IPipeClient pipeClient, InstanceOptions instance, ILogger logger, int delayMs, string styleSheet)
    {
        if (await pipeClient.IsDisabled(topics => topics.RestartService, logger))
        {
            LogRestartDisabled(logger);
            return Results.Content(RestartDisabledPage.Create(styleSheet), MediaTypeNames.Text.Html);
        }

        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(delayMs);
                LogRestartRequested(logger);

                await pipeClient.RestartSuite(instance);
            }
            catch (Exception ex)
            {
                LogRestartFailed(logger, ex);
            }
        });

        return Results.Redirect("/");
    }

    [LoggerMessage(LogLevel.Warning, "HostManagement disables RestartService, asking for a device restart")]
    private static partial void LogRestartDisabled(ILogger logger);

    [LoggerMessage(LogLevel.Information, "Stop delay passed by. Requesting restart by hostmanagement now...")]
    private static partial void LogRestartRequested(ILogger logger);

    [LoggerMessage(LogLevel.Error, "Failed to trigger restart")]
    private static partial void LogRestartFailed(ILogger logger, Exception ex);
}
