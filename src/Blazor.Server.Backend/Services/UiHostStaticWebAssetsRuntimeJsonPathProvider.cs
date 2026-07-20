using Core.UiHosting;
using Microsoft.Extensions.Logging;
using ViciOne.Ui.MonochromeIcons.Assets.Services;

namespace Blazor.Server.Backend.Services;

internal sealed partial class UiHostStaticWebAssetsRuntimeJsonPathProvider(IUiHostEnvironment uiHostEnvironment,
    ILogger<UiHostStaticWebAssetsRuntimeJsonPathProvider> logger) : IStaticWebAssetsRuntimeJsonPathProvider
{
    public string GetPath()
    {
        var path = uiHostEnvironment.GetWwwRootFolder();

        if (string.IsNullOrWhiteSpace(path))
            throw new InvalidOperationException("UiHost static web assets path is invalid.");

        LogStaticWebAssetsPath(logger, path, uiHostEnvironment.IsDevelopment);

        return path;
    }

    [LoggerMessage(Level = LogLevel.Trace, Message = "Static web assets path: '{Path}'. Development: {IsDevelopment}.")]
    private static partial void LogStaticWebAssetsPath(ILogger logger, string path, bool isDevelopment);
}
