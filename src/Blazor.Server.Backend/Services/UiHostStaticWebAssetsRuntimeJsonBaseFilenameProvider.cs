using ViciOne.Ui.MonochromeIcons.Assets.Services;

namespace Blazor.Server.Backend.Services;

internal sealed class UiHostStaticWebAssetsRuntimeJsonBaseFilenameProvider
    : IStaticWebAssetsRuntimeJsonBaseFilenameProvider
{
    private readonly string _baseFilename =
        typeof(BlazorServerBackendModule).Assembly.GetName().Name ?? string.Empty;

    public string GetBaseFilename()
        => _baseFilename;
}
