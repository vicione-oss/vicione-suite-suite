namespace Core.OS.Hosting.Pages;

/// <summary>
/// Answers an action on a startup failure page when HostManagement disables RestartService. The action
/// itself has already happened, so blocking it would only bring back the same page after the next device start.
/// </summary>
internal static class RestartDisabledPage
{
    public const string Text = "The Suite cannot restart because this is disabled in HostManagement. Restart the device to continue.";

    /// <param name="styleSheet">The stylesheet of the calling page, which its Content Security Policy already allows.</param>
    public static string Create(string styleSheet) => $"""
        <!doctype html>
        <html lang="en">
        <head>
            <meta charset="UTF-8">
            <title>Restart not possible</title>
            <style>{styleSheet}</style>
        </head>
        <body>
        <main>
            <h1>Restart not possible</h1>
            <p>{Text}</p>
        </main>
        </body>
        </html>
        """;
}
