using Blazor.Shared.SystemInformation.Enums;
using Sdk.Client.Colors;
using Sdk.Client.Extensions;

namespace Blazor.Shared.SystemInformation.Colors;

public sealed class LogCenterHtmlColor : IHtmlColor
{
    private readonly LogCenterColor _logCenterColor;
    private readonly string _cssCustomVariable;

    private LogCenterHtmlColor(LogCenterColor color)
    {
        _logCenterColor = color;
        _cssCustomVariable = $"var(--log-center-color-{_logCenterColor.ToString().ToHyphenSeparated()})";
    }

    public static LogCenterHtmlColor From(LogCenterColor color) => new(color);

    public override string ToString() => _cssCustomVariable;
}
