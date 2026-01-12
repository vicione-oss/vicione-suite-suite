using System.IO.Abstractions;
using System.IO.Abstractions.TestingHelpers;
using Core.OS.Instance;
using Core.OS.Instance.Extensions;
using Core.OS.Instance.Services;
using Core.OS.Modules;
using Core.OS.Tests.Extensions;
using Core.OS.Tests.Instance.Extensions;
using AwesomeAssertions;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Sdk.Instance;
using Sdk.Testing.Backend;
using Xunit;

namespace Core.OS.Tests.Instance.Services;

public class BackupBuilderTests
{
    private readonly string _appDataPath = "\\path\\to\backup";
    private readonly string _destinationPath = "\\path\\to\\destination";

    private ServiceProvider CreateServices(Action<IServiceCollection>? configure = null)
    {
        var instanceInformationProvider = Substitute.For<ILocalInstanceInformationProvider>();
        instanceInformationProvider.SetupLocalInstanceInformation();

        var services = new ServiceCollection()
            .AddSingleton(instanceInformationProvider);

        configure?.Invoke(services);

        return services.BuildServiceProvider();
    }

    public class Metadata : BackupBuilderTests
    {
        [Fact]
        public async Task Should_add_backup_metadata_always()
        {
            // Arrange
            await using var services = CreateServices();
            await using var memoryStream = new MemoryStream();
            var localInstance = services.GetRequiredService<ILocalInstanceInformationProvider>().Local;

            var builder = new BackupBuilder();

            // Act
            var result = await builder.BuildBackup(services, memoryStream, CancellationToken.None);

            // Assert
            result.ShouldContainInstanceInfos(localInstance);
        }
    }

    public class UseSystemModuleBackup : BackupBuilderTests
    {
        [Trait(Traits.Category, Traits.System)]
        [Fact]
        public async Task Should_add_system_module_to_archive()
        {
            // Arrange
            await using var services = CreateServices(configure =>
            {
                configure.SetupSuiteModuleBackup(_appDataPath);
            });

            await using var memoryStream = new MemoryStream();
            var builder = new BackupBuilder()
                .UseSystemModuleBackup();

            // Act
            var result = await builder.BuildBackup(services, memoryStream, CancellationToken.None);

            // Assert
            result.SystemModule.Should().NotBeNull();
            result.SystemModule!.Error.Should().BeNull();
        }
    }

    public class UseModuleBackup : BackupBuilderTests
    {
        [Trait(Traits.Category, Traits.System)]
        [Fact]
        public async Task Should_add_all_installed_modules_to_archive()
        {
            // Arrange
            await using var services = CreateServices(configure =>
            {
                configure.SetupSuiteModuleBackup(_appDataPath);
            });

            await using var memoryStream = new MemoryStream();
            var builder = new BackupBuilder()
                .UseModuleBackup();

            var installedModules = await services.GetRequiredService<IModuleMetadataCache>()
                .GetInstalledModuleMetadata();

            // Act
            var result = await builder.BuildBackup(services, memoryStream, CancellationToken.None);

            // Assert
            result.SystemModule.Should().NotBeNull();
            result.SystemModule!.Error.Should().BeNull();
            result.Modules.Should().AllSatisfy(k => k.Error.Should().BeNull());
            result.Modules.Should().HaveCount(installedModules.Count);
        }
    }

    public class UseSystemConfigurationBackup : BackupBuilderTests
    {
        [Trait(Traits.Category, Traits.System)]
        [Fact]
        public async Task Should_add_system_configuration_to_archive_file()
        {
            // Arrange
            await using var services = CreateServices(configure =>
            {
                configure.SetupSystemConfiguration();
            });
            var builder = new BackupBuilder()
                .UseSystemConfigurationBackup();

            // Act
            var result = await builder.BuildBackup(services, _destinationPath, CancellationToken.None);

            // Assert
            result.SystemConfiguration.Should().NotBeNull();
            result.SystemConfiguration!.Error.Should().BeNull();
        }

        [Fact]
        public async Task Should_add_system_configuration_to_archive()
        {
            // Arrange
            await using var services = CreateServices(configure =>
            {
                configure.SetupSystemConfiguration();
            });
            await using var memoryStream = new MemoryStream();
            var builder = new BackupBuilder()
                .UseSystemConfigurationBackup();

            // Act
            var result = await builder.BuildBackup(services, memoryStream, CancellationToken.None);

            // Assert
            result.SystemConfiguration.Should().NotBeNull();
            result.SystemConfiguration!.Error.Should().BeNull();
        }
    }

    public class UseOverrideExistingBackup : BackupBuilderTests
    {
        private const string BackupFileName = "my-backup.zip";
        private const string BackupPath = "/destination";

        [Fact]
        public async Task Should_override_existing_backup()
        {
            // Arrange
            await using var services = CreateServices(SetupBackupFile);
            var builder = new BackupBuilder()
                .UseBackupFileName(BackupFileName)
                .UseOverrideExistingBackup();

            // Act
            var result = await builder.BuildBackup(services, BackupPath, CancellationToken.None);

            // Assert
            result.InstanceId.Should().NotBeEmpty();
            result.Name.Should().NotBeNullOrEmpty();
            result.SuiteVersion.Should().NotBeNullOrEmpty();
            result.SdkVersion.Should().NotBeNullOrEmpty();
            result.InstanceType.Should().NotBeNullOrEmpty();
        }

        [Fact]
        public async Task Should_fail_without_override_option_if_backup_exists()
        {
            // Arrange
            var services = CreateServices(SetupBackupFile);
            var builder = new BackupBuilder()
                .UseBackupFileName(BackupFileName);

            // Act
            var action = FluentActions.Awaiting(() => builder.BuildBackup(services, BackupPath, CancellationToken.None));

            // Assert
            await action.Should().ThrowAsync<InvalidOperationException>().WithMessage(@"*already exists*");
            await services.DisposeAsync();
        }

        private static void SetupBackupFile(IServiceCollection services)
        {
            var fileSystem = new MockFileSystem();
            fileSystem.AddDirectory(BackupPath);
            fileSystem.AddEmptyFile(fileSystem.Path.Combine(BackupPath, BackupFileName));

            services.AddSingleton<IFileSystem>(fileSystem);
        }
    }

    public class FullBackup : BackupBuilderTests
    {
        [Trait(Traits.Category, Traits.System)]
        [Fact]
        public async Task Should_add_all_features_to_archive_file()
        {
            // Arrange
            var informationProvider = Substitute.For<IInstanceInformationProvider>();
            await using var services = CreateServices(configure =>
            {
                configure
                    .AddSingleton<IFileSystem>(new FileSystem())
                    .AddSingleton(informationProvider)
                    .SetupSuiteModuleBackup(_appDataPath)
                    .SetupSystemConfiguration();
            });

            var fileSystem = services.GetRequiredService<IFileSystem>();
            var instanceIdInfo = fileSystem.Path.Combine(_appDataPath, IFileSystemExtensions.InstanceIdFileName);
            var instanceId = Guid.Parse(await fileSystem.File.ReadAllTextAsync(instanceIdInfo));
            informationProvider.SetupGetInstanceInformation(instanceId, InstanceType.Standalone);

            var builder = new BackupBuilder()
                .UseModuleBackup()
                .UseSystemConfigurationBackup()
                .UseOverrideExistingBackup();

            // Act
            var result = await builder.BuildBackup(services, _destinationPath, CancellationToken.None);

            // Assert
            var local = services.GetRequiredService<ILocalInstanceInformationProvider>().Local;

            result.ShouldContainInstanceInfos(local);
            result.SystemModule.Should().NotBeNull();
            result.SystemConfiguration.Should().NotBeNull();
            result.Modules.Should().AllSatisfy(k => k.Error.Should().BeNull());
            result.Modules.SelectMany(k => k.Databases ?? []).Should().AllSatisfy(k => k.Error.Should().BeNull());
        }
    }
}
