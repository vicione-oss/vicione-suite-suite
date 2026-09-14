using System.Reflection;

namespace Core.OS.Hosting.Pages;

/// <summary>
/// The stylesheets of the two pages that answer when the suite did not come up. Each page renders
/// its sheet inline and the Content Security Policy hashes the very same string, so the two cannot
/// drift apart. They are embedded rather than served as static assets because a page that reports a
/// failed startup must be readable without the asset pipeline the failure may have taken with it.
/// </summary>
internal static class StartupFailurePageStyles
{
    public static string FailsafePage { get; } = Read("failsafe-page.css");

    public static string DowngradePage { get; } = Read("downgrade-page.css");

    private static string Read(string fileName)
    {
        var resource = $"{typeof(StartupFailurePageStyles).Namespace}.{fileName}";
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(resource)
            ?? throw new InvalidOperationException($"Can't find embedded resource '{resource}'");

        using var reader = new StreamReader(stream);

        return reader.ReadToEnd();
    }
}
