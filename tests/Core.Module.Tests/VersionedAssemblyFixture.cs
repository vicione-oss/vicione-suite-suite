using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Emit;

namespace Core.Module.Tests;

/// <summary>
/// Roslyn-based helper that generates real, on-disk, versioned assemblies used to exercise the
/// custom <see cref="ModuleAssemblyLoadContext"/> resolution logic.
///
/// It produces a shared dependency assembly (<c>SharedDep.dll</c>) at an arbitrary
/// <see cref="AssemblyVersion"/> together with a tiny "module" assembly that references it.
/// Each module is emitted into its own isolated directory with a hand-authored
/// <c>*.deps.json</c> (libraries of type <c>project</c>) so that the runtime's
/// <see cref="System.Runtime.Loader.AssemblyDependencyResolver"/> resolves the sibling
/// <c>SharedDep.dll</c> that lives next to the module.
/// </summary>
internal sealed class VersionedAssemblyFixture : IDisposable
{
    private const string SharedDepAssemblyName = "SharedDep";

    private readonly string _rootPath;

    public VersionedAssemblyFixture()
    {
        _rootPath = Path.Combine(Path.GetTempPath(), $"vo-loadctx-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_rootPath);
    }

    /// <summary>
    /// Emits a module assembly (with the given name) that references a <c>SharedDep</c> assembly
    /// stamped with <paramref name="sharedDepVersion"/>. The module dll, the shared dll and a
    /// matching deps.json are written into a dedicated sub folder.
    /// </summary>
    /// <returns>The full path to the emitted module assembly.</returns>
    public string CreateModule(string moduleName, Version sharedDepVersion)
    {
        var moduleFolder = Path.Combine(_rootPath, moduleName);
        Directory.CreateDirectory(moduleFolder);

        var references = GetRuntimeReferences().ToList();

        // 1) compile the shared dependency with the requested assembly version
        var sharedDepPath = Path.Combine(moduleFolder, $"{SharedDepAssemblyName}.dll");
        var sharedDepSource = $$"""
            using System.Reflection;
            [assembly: AssemblyVersion("{{sharedDepVersion}}")]
            namespace SharedDep;
            public static class SharedThing
            {
                public static string Value => "shared";
            }
            """;
        EmitAssembly(SharedDepAssemblyName, sharedDepSource, references, sharedDepPath);

        // 2) compile the module which references the shared dependency
        var modulePath = Path.Combine(moduleFolder, $"{moduleName}.dll");
        var moduleSource = $$"""
            namespace {{moduleName}};
            public static class Entry
            {
                public static string Describe() => SharedDep.SharedThing.Value;
            }
            """;
        var moduleReferences = references.Append(MetadataReference.CreateFromFile(sharedDepPath));
        EmitAssembly(moduleName, moduleSource, moduleReferences, modulePath);

        // 3) write a minimal deps.json so AssemblyDependencyResolver can resolve the shared dep
        WriteDepsJson(moduleFolder, moduleName, SharedDepAssemblyName);

        return modulePath;
    }

    private static void EmitAssembly(string assemblyName, string source, IEnumerable<MetadataReference> references, string outputPath)
    {
        var syntaxTree = CSharpSyntaxTree.ParseText(source);
        var compilation = CSharpCompilation.Create(
            assemblyName,
            [syntaxTree],
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, optimizationLevel: OptimizationLevel.Release));

        EmitResult result;
        using (var fileStream = File.Create(outputPath))
        {
            result = compilation.Emit(fileStream);
        }

        if (!result.Success)
        {
            var errors = string.Join(Environment.NewLine, result.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));
            throw new InvalidOperationException($"Failed to emit '{assemblyName}':{Environment.NewLine}{errors}");
        }
    }

    private static void WriteDepsJson(string moduleFolder, string moduleName, string sharedDepName)
    {
        const string tfm = ".NETCoreApp,Version=v10.0";
        var depsJson = $$"""
            {
              "runtimeTarget": {
                "name": "{{tfm}}",
                "signature": ""
              },
              "compilationOptions": {},
              "targets": {
                "{{tfm}}": {
                  "{{moduleName}}/1.0.0": {
                    "dependencies": {
                      "{{sharedDepName}}": "1.0.0"
                    },
                    "runtime": {
                      "{{moduleName}}.dll": {}
                    }
                  },
                  "{{sharedDepName}}/1.0.0": {
                    "runtime": {
                      "{{sharedDepName}}.dll": {}
                    }
                  }
                }
              },
              "libraries": {
                "{{moduleName}}/1.0.0": {
                  "type": "project",
                  "serviceable": false,
                  "sha512": ""
                },
                "{{sharedDepName}}/1.0.0": {
                  "type": "project",
                  "serviceable": false,
                  "sha512": ""
                }
              }
            }
            """;

        File.WriteAllText(Path.Combine(moduleFolder, $"{moduleName}.deps.json"), depsJson);
    }

    private static IEnumerable<MetadataReference> GetRuntimeReferences()
    {
        var trustedAssemblies = (AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") as string) ?? string.Empty;

        return trustedAssemblies
            .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)
            .Where(path => path.EndsWith(".dll", StringComparison.OrdinalIgnoreCase) && File.Exists(path))
            .Select(path => (MetadataReference)MetadataReference.CreateFromFile(path));
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_rootPath))
                Directory.Delete(_rootPath, recursive: true);
        }
        catch (IOException)
        {
            // Best-effort cleanup: a loaded assembly can keep files locked on some platforms.
        }
        catch (UnauthorizedAccessException)
        {
            // Best-effort cleanup.
        }
    }
}
