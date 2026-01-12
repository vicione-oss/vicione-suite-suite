using System.IO.Abstractions;
using Core.Module.Extensions;

namespace Suite.Deps;

internal static class ModulesAnalyser
{
    public static Task Execute(ModulesAnalyserOptions options)
    {
        var context = SuiteDependencyContextFactory.Create(new FileSystem(), options.SuitePath, options.Manifest);
        if (context.UiHosts.Count <= 0)
            throw new InvalidOperationException("No UiHost (Blazor.Server|Wasm) was found");

        var paths = context.GetAllRedundantPaths();
        foreach (var path in paths)
        {
            Console.WriteLine(options.RelativePaths ? Path.GetRelativePath(options.SuitePath!, path) : path);
        }

        return Task.CompletedTask;
    }
}
