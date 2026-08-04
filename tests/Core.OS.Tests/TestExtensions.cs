using System.IO.Abstractions;
using System.IO.Abstractions.TestingHelpers;
using System.Reflection;
using System.Text.Json;
using Core.Module;
using Core.OS.Instance;
using Core.OS.Instance.Extensions;
using Core.OS.Tests.Extensions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Sdk.Instance;
using Sdk.Modules;

namespace Core.OS.Tests;

internal static class TestExtensions
{
    internal static IServiceCollection AddInstanceServicesMock(this IServiceCollection services, InstanceType instanceType = InstanceType.Standalone)
    {
        var localProviderMock = Substitute.For<ILocalInstanceInformationProvider>();
        localProviderMock.SetupLocalInstanceInformation(instanceType);

        var providerMock = Substitute.For<IInstanceInformationProvider>();
        providerMock.Local
            .Returns(localProviderMock.Local);

        return services
            .AddTransient(s => s.GetRequiredService<IConfiguration>().GetInstanceOptions())
            .AddSingleton(localProviderMock)
            .AddScoped(_ => providerMock);
    }

    internal static void SetupInstanceIdFile(this IFileSystem fileSystem, Guid instanceId, string appDirectory = "App")
    {
        fileSystem.Path.IsPathRooted(appDirectory).Returns(true);
        fileSystem.Path.Combine(Arg.Any<string>(), OS.Instance.Extensions.IFileSystemExtensions.InstanceIdFileName).Returns(nameof(instanceId));
        fileSystem.File.ReadAllText(nameof(instanceId)).Returns(instanceId.ToString());
    }

    /// <summary>
    /// Setup up a local meta
    /// </summary>
    /// <param name="fileSystem"></param>
    /// <param name="moduleMetadata"></param>
    internal static void SetupModuleMetadataJson(this MockFileSystem fileSystem, ModuleMetadata moduleMetadata, string? targetPath = null)
    {
        var testAssembly = Assembly.GetExecutingAssembly();

        if (string.IsNullOrEmpty(targetPath))
            targetPath = fileSystem.Path.GetDirectoryName(testAssembly.Location);

        var metadataPath = fileSystem.Path.Combine(targetPath!, $"{moduleMetadata.Name}.meta.json");
        var metadata = JsonSerializer.Serialize(moduleMetadata, ModuleSerializerOptions.GetOptions());
        var metadataMock = new MockFileData(metadata);

        fileSystem.AddFile(metadataPath, metadataMock);
    }
}
