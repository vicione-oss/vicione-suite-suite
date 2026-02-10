using System.Diagnostics;

namespace Core.OS.Diagnostics;

internal static class CoreActivitySource
{
    public const string SourceName = "ViciOne.Suite.Core.OS";

    public static readonly ActivitySource Source = new(SourceName);
}
