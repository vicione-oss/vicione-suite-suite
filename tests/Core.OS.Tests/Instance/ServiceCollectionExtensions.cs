using AutoFixture;
using Core.OS.Modules;
using Core.OS.Modules.Contracts;
using Core.Shared.HostManagement;
using Core.Shared.Modules.Contracts;
using HostManagement.Shared.Contracts;
using Microsoft.Extensions.DependencyInjection;
using Sdk.Backend.Messaging;
using Sdk.Modules;
using Sdk.Testing.Backend;

namespace Core.OS.Tests.Instance;

public static class ServiceCollectionExtensions
{
    extension(IServiceCollection services)
    {
        public IServiceCollection SetupSuiteModuleBackup(string appDataPath)
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

        private IServiceCollection SetupSuiteModuleBackup(Dictionary<string, ModuleMetadata> moduleOptions)
        {
            var workspaceManagement = Substitute.For<IWorkspaceManagement>();

            foreach (var option in moduleOptions)
            {
                workspaceManagement.GetHomeDirectory(option.Value.Name).Returns(option.Key);
            }

            // system module special - it's not an installed module (maybe add a hide option to it once)
            var metadataProvider = Substitute.For<IModuleMetadataProvider>();
            var installedModules = moduleOptions
                .Where(d => d.Value.Name != Shared.Constants.SystemModuleId)
                .Select(k => new ModuleMetadataBundle
                {
                    ModuleId = k.Value.Name,
                    Metadata = k.Value,
                    Installed = true,
                })
                .ToList();
            
            metadataProvider
                .GetModuleMetadata(Arg.Any<GetModuleMetadataOptions>(), Arg.Any<CancellationToken>())
                .Returns(installedModules);

            services.AddSingleton(workspaceManagement);
            services.AddSingleton(metadataProvider);

            return services;
        }

        public IServiceCollection SetupSystemConfiguration(SystemConfiguration? systemConfiguration = null)
        {
            var fixture = new Fixture();
            systemConfiguration ??= fixture.Create<SystemConfiguration>();

            var mediator = Substitute.For<ISuiteMediator>();
            mediator.SetupRequest(
                new GetHostMgmtSystemConfiguration(),
                new GetHostMgmtSystemConfigurationResponse
                {
                    Configuration = systemConfiguration
                });

            services.AddSingleton(mediator);

            return services;
        }
    }
}
