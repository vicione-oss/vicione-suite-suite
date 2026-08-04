using System.IO.Abstractions.TestingHelpers;
using System.Reflection;
using Core.Module.Utils;
using Sdk.Modules;
using TestModule.Backend;
using TestModule.Client;
using TestSystem.Backend;
using TestUiHost;

namespace Core.Module.Tests;

internal static class TestMockFileSystemExtensions
{
    extension(MockFileSystem fileSystem)
    {
        public MockFileSystem SetupTestCore(bool realDepsJson = true, bool realDll = false)
        {
            var assembly = typeof(TestSystemModule).Assembly;
            Assert.NotNull(assembly);

            var moduleDirectory = fileSystem.Path.GetDirectoryName(assembly.Location);
            Assert.NotNull(moduleDirectory);

            var depsPath = fileSystem.Path.Combine(moduleDirectory, assembly.GetName().Name + ".deps.json");
            fileSystem.AddFile(depsPath, CreateMockDepsJsonCopy(realDepsJson ? assembly : null));

            var dllPath = fileSystem.Path.Combine(moduleDirectory, assembly.GetName().Name + ".dll");
            fileSystem.AddFile(dllPath, CreateMockDllCopy(realDll ? assembly : null));

            return fileSystem;
        }

        public MockFileSystem SetupTestUiHost(string modulePath, bool realDepsJson = true, bool realDll = false)
            => fileSystem.SetupModule<TestUiHostBackend>(modulePath, ModuleIdResolver.ResolveId<TestUiHostBackend>(), realDepsJson, realDll);

        public MockFileSystem SetupTestBackendModule(string modulePath, bool realDepsJson = true, bool realDll = false)
            => fileSystem.SetupModule<TestBackendModule>(modulePath, ModuleIdResolver.ResolveId<TestBackendModule>(), realDepsJson, realDll);

        public MockFileSystem SetupTestClientModule(string modulePath, bool realDepsJson = true, bool realDll = false)
            => fileSystem.SetupModule<TestClientModule>(modulePath, ModuleIdResolver.ResolveId<TestClientModule>(), realDepsJson, realDll);

        private MockFileSystem SetupModule<T>(string modulePath, string moduleId, bool realDepsJson = true, bool realDll = false)
        {
            var assembly = typeof(T).Assembly;
            Assert.NotNull(assembly);

            var moduleDirectory = fileSystem.Path.Combine(modulePath, moduleId);
            fileSystem.AddDirectory(moduleDirectory);

            var dllPath = fileSystem.Path.Combine(moduleDirectory, assembly.GetName().Name + ".dll");
            fileSystem.AddFile(dllPath, CreateMockDllCopy(realDll ? assembly : null));

            var depsPath = fileSystem.Path.Combine(moduleDirectory, assembly.GetName().Name + ".deps.json");
            fileSystem.AddFile(depsPath, CreateMockDepsJsonCopy(realDepsJson ? assembly : null));

            return fileSystem;
        }
    }

    private static MockFileData CreateMockDllCopy(Assembly? assembly = null)
        => CreateMockFileCopy(assembly?.Location);

    private static MockFileData CreateMockDepsJsonCopy(Assembly? assembly = null)
        => CreateMockFileCopy(assembly != null ? ModuleHelpers.DllToDepsJson(assembly.Location) : null);

    private static MockFileData CreateMockFileCopy(string? realFilePath)
    {
        if (string.IsNullOrEmpty(realFilePath))
            return new([]);

        using var assemblyStream = new FileStream(realFilePath, FileMode.Open, FileAccess.Read);
        using var ms = new MemoryStream();
        assemblyStream.CopyTo(ms);

        return new(ms.ToArray());
    }
}
