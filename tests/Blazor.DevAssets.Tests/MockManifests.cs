using System.IO.Abstractions.TestingHelpers;
using System.Text.Json.Nodes;

namespace Blazor.DevAssets.Tests;

/// <summary>
/// Writes staticwebassets.runtime.json files, and the files they serve, into a <see cref="MockFileSystem"/>.
/// </summary>
internal sealed class MockManifests
{
    private static readonly string Root = OperatingSystem.IsWindows() ? @"C:\devassets" : "/devassets";

    public MockFileSystem FileSystem { get; } = new();

    /// <summary>
    /// The folder that holds the manifests.
    /// </summary>
    public string Folder => Root;

    /// <summary>
    /// Writes {name}.staticwebassets.runtime.json with one content root that holds each file, served at its route.
    /// </summary>
    /// <returns>The path of the manifest.</returns>
    public string Write(string name, params (string Route, string File)[] assets)
    {
        var contentRoot = FileSystem.Path.Combine(Root, name) + FileSystem.Path.DirectorySeparatorChar;
        var root = new JsonObject();

        foreach (var (route, file) in assets)
        {
            FileSystem.AddFile(FileSystem.Path.Combine(contentRoot, file), new MockFileData(route));

            var node = root;
            foreach (var segment in route.Split('/'))
            {
                var children = (JsonObject)(node["Children"] ??= new JsonObject());
                node = (JsonObject)(children[segment] ??= new JsonObject());
            }

            node["Asset"] = new JsonObject { ["ContentRootIndex"] = 0, ["SubPath"] = file };
        }

        var manifestPath = FileSystem.Path.Combine(Root, $"{name}.staticwebassets.runtime.json");
        var manifest = new JsonObject { ["ContentRoots"] = new JsonArray(contentRoot), ["Root"] = root };
        FileSystem.AddFile(manifestPath, new MockFileData(manifest.ToJsonString()));

        return manifestPath;
    }
}
