using System.IO.Abstractions;
using Sdk.Testing.Extensions;

namespace Core.Tests.Tools;

public static class PathHelpers
{
    public static string GetSourcePath() => Path.Combine(GetRootPath(), "src");
    public static string GetAppSettingsFilePath() => Path.Combine([GetRootPath(), "src", "Core.OS", "appsettings.json"]);

    private static string GetRootPath() => new FileSystem().GetPathContaining(
    [
        "README.md",
        "CHANGELOG.md"
    ], "*.md");
}
