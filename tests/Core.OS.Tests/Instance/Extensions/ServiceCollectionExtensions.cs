using AutoFixture;
using Core.OS.Modules;
using Core.Shared.Modules.Contracts;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Sdk.Backend.Messaging;
using Sdk.Modules;
using Sdk.SystemConfiguration.Contracts;
using Sdk.SystemConfiguration.Requests;
using Sdk.Testing.Backend;

namespace Core.OS.Tests.Instance.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection SetupSuiteModuleBackup(this IServiceCollection services, string appDataPath)
    {
        var moduleOptions = Directory
            .GetDirectories(appDataPath)
            .ToDictionary(d => d,
                d => new ModuleMetadata
                {
                    Name = Path.GetFileName(d),
                    Version = "1.2.3",
                    MinSuiteSdkVersion = "0.29.0",
                    Title = Path.GetFileName(d),
                });

        return services.SetupSuiteModuleBackup(moduleOptions);
    }

    public static IServiceCollection SetupSuiteModuleBackup(this IServiceCollection services, Dictionary<string, ModuleMetadata> moduleOptions)
    {
        var workspaceManagement = Substitute.For<IWorkspaceManagement>();

        foreach (var option in moduleOptions)
        {
            workspaceManagement.GetHomeDirectory(option.Value.Name).Returns(option.Key);
        }

        // system module special - it's not an installed module (maybe add a hide option to it once)
        var metadataCache = Substitute.For<IModuleMetadataCache>();
        var installedModules = moduleOptions
            .Where(d => d.Value.Name != Sdk.Constants.SystemModuleId)
            .Select(k => new ModuleMetadataBundle
            {
                ModuleId = k.Value.Name,
                Metadata = k.Value,
                Installed = true,
            })
            .ToList();
        metadataCache.GetInstalledModuleMetadata().Returns(installedModules);

        services.AddSingleton(workspaceManagement);
        services.AddSingleton(metadataCache);

        return services;
    }

    public static IServiceCollection SetupSystemConfiguration(this IServiceCollection services, SystemConfiguration? systemConfiguration = null)
    {
        var fixture = new Fixture();
        systemConfiguration ??= fixture.Create<SystemConfiguration>();

        var mediator = Substitute.For<ISuiteMediator>();
        mediator.SetupRequest(
            new GetSystemConfiguration(),
            new GetSystemConfigurationResponse
            {
                Configuration = systemConfiguration
            });

        services.AddSingleton(mediator);

        return services;
    }
}
