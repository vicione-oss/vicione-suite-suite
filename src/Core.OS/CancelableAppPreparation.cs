using Serilog;

namespace Core.OS;

internal static class CancelableAppPreparation
{
    /// <summary>
    /// Use this to run a task that should be stoppable before webapp
    /// is built when application gets killed, closed etc.
    /// </summary>
    /// <param name="preparation"></param>
    /// <returns></returns>
    public static async Task Execute(Func<CancellationToken, Task> preparation)
    {
        using var startupCts = new CancellationTokenSource();

        // these are available from startup
        Console.CancelKeyPress += OnConsoleCancel;
        AppDomain.CurrentDomain.ProcessExit += OnCurrentDomainOnProcessExit;

        try
        {
            await preparation.Invoke(startupCts.Token);
        }
        catch (TaskCanceledException)
        {
            Log.Logger.Warning("Startup preparation was canceled by process exit");
        }

        // ensure we disconnect them to avoid interference with ApplicationLifetime
        Console.CancelKeyPress -= OnConsoleCancel;
        AppDomain.CurrentDomain.ProcessExit -= OnCurrentDomainOnProcessExit;

        return;

        void OnCurrentDomainOnProcessExit(object? o, EventArgs eventArgs)
            => startupCts.Cancel();

        void OnConsoleCancel(object? o, ConsoleCancelEventArgs eventArgs)
        {
            eventArgs.Cancel = true;
            startupCts.Cancel();
        }
    }
}
