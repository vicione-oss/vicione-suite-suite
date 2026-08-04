using System.IO.Abstractions.TestingHelpers;
using Core.OS.HostManagement;
using Core.OS.Instance;
using Core.Shared.HostManagement;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Core.OS.Tests.HostManagement.Extensions;

internal static class ServiceCollectionExtensions
{
    public static IServiceCollection AddMockPipeClientSystemConfiguration(this IServiceCollection services)
    {
        var fileSystem = new MockFileSystem();
        fileSystem.AddGetSystemConfigurationResult();

        var appLifetime = Substitute.For<Microsoft.Extensions.Hosting.IHostApplicationLifetime>();

        var options = new HostManagementOptions();
        var mockOptions = new MockPipeClientOptions
        {
            Enabled = true,
            DataSource = MockPipeClientDataSource.SystemConfigurationJsonFile,
            SystemConfigurationJsonFile = fileSystem.AllFiles.First(),
        };

        var instanceOptions = new InstanceOptions
        {
            HomeDirectory = fileSystem.Path.GetFullPath("AppData"),
            CacheDirectory = fileSystem.Path.GetFullPath("Cache"),
            BackupDirectory = fileSystem.Path.GetFullPath("Backup"),
            Type = Sdk.Instance.InstanceType.Standalone,
        };

        services
            .AddSingleton<IPipeClient>(_ => new TestPipeClient(new MockPipeClient(fileSystem, appLifetime, Options.Create(mockOptions), Options.Create(instanceOptions))))
            .AddSingleton(Options.Create(options))
            .AddSingleton(Options.Create(instanceOptions));

        return services;
    }
}
