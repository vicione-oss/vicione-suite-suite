using Core.UiHosting;
using Microsoft.Extensions.Logging;
using ViciOne.Ui.MonochromeIcons.Assets.Services;

namespace Blazor.Server.Backend.Services;

internal sealed partial class UiHostFileSystemBasedContentRootProvider(IUiHostEnvironment uiHostEnvironment,
    ILogger<UiHostFileSystemBasedContentRootProvider> logger) : IFileSystemBasedContentRootProvider
{
    private const string MonochromeIconsAssetsContentPath = "ViciOne.Ui.MonochromeIcons.Assets";

    public string? GetContentRoot()
    {
        var wwwRootFolder = uiHostEnvironment.GetWwwRootFolder();

        if (string.IsNullOrWhiteSpace(wwwRootFolder))
            return null;

        var contentRoot = Path.Combine(wwwRootFolder, "_content", MonochromeIconsAssetsContentPath);

        LogContentRoot(logger, contentRoot, uiHostEnvironment.IsDevelopment);

        return Directory.Exists(contentRoot)
            ? contentRoot
            : null;
    }

    [LoggerMessage(Level = LogLevel.Trace, Message = "MonochromeIcons content root: '{ContentRoot}'. Development: {IsDevelopment}.")]
    private static partial void LogContentRoot(ILogger logger, string? contentRoot, bool isDevelopment);
}
