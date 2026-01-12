using System.IO.Abstractions.TestingHelpers;
using System.Reflection;
using Core.OS.HostManagement;

namespace Core.OS.Tests.HostManagement.Extensions;

internal static class MockFileSystemExtensions
{
    public static MockFileSystem AddGetSystemConfigurationResult(this MockFileSystem fileSystem, string path = "GetSystemConfiguration.json")
    {
        fileSystem.AddFileFromEmbeddedResource(path, Assembly.GetAssembly(typeof(MockPipeClient)), MockPipeClient.GetSystemConfigurationResultResource);

        return fileSystem;
    }
}
