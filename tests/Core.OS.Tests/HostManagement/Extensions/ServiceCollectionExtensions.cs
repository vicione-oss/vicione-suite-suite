using System.IO.Abstractions.TestingHelpers;
using Core.OS.HostManagement;
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

        var options = new HostManagementOptions();
        var mockOptions = new MockPipeClientOptions
        {
            Enabled = true,
            DataSource = MockPipeClientDataSource.SystemConfigurationJsonFile,
            SystemConfigurationJsonFile = fileSystem.AllFiles.First(),
        };

        services
            .AddSingleton<IPipeClient>(_ => new MockPipeClient(fileSystem, Options.Create(mockOptions)))
            .AddSingleton(Options.Create(options));

        return services;
    }
}
