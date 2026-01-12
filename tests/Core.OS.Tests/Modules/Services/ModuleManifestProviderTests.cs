using System.IO.Abstractions;
using System.IO.Abstractions.TestingHelpers;
using System.Reflection;
using System.Text.Json;
using Core.OS.Instance;
using Core.OS.Modules;
using Core.OS.Modules.Extensions;
using Core.OS.Modules.Services;
using AwesomeAssertions;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Sdk.Messaging;
using Sdk.Modules;
using Xunit;
using Assert = Xunit.Assert;

namespace Core.OS.Tests.Modules.Services;

public class ModuleManifestProviderTests
{
    private const string ModuleA = "ModuleA";
    private const string ModuleB = "ModuleB";
    private const string ModuleC = "ModuleC";

    private readonly Serilog.ILogger _logger = Substitute.For<Serilog.ILogger>();
    private readonly MockFileSystem _fileSystem = new();
    private readonly InstanceOptions _instanceOptions = new()
    {
        HomeDirectory = "AppData",
        CacheDirectory = "Cache",
        BackupDirectory = "Backup",
        Type = Sdk.Instance.InstanceType.Standalone,
    };

    public ServiceProvider SetupServiceProvider()
        => new ServiceCollection()
            .AddSingleton(_logger)
            .AddSingleton<IFileSystem>(_fileSystem)
            .AddSingleton(_instanceOptions)
            .AddSingleton<ModuleManifestProvider>()
            .BuildServiceProvider();

    private void SetupPackageManifestFile(out string filePath, out ModulePackageManifest manifest)
    {
        filePath = _fileSystem.GetModuleVersionsFilePath(_instanceOptions);
        manifest = new ModulePackageManifest
        {
            Packages = [
                new ModuleDependencyPackage { Name = ModuleA, Version = "0.1.0"},
                new ModuleDependencyPackage { Name = ModuleB, Version = "1.3.0"},
                new ModuleDependencyPackage { Name = ModuleC, Version = "2.5.7"},
            ]
        };

        var parent = _fileSystem.Path.GetDirectoryName(filePath);

        _fileSystem.AddDirectory(parent);
        _fileSystem.AddFile(filePath, new MockFileData(JsonSerializer.Serialize(manifest, DefaultJsonSerializerSettings.Default)));
    }

    public class LoadPackageManifest : ModuleManifestProviderTests
    {
        [Fact]
        public async Task Should_use_existing_modules_json()
        {
            // Arrange
            await using var serviceProvider = SetupServiceProvider();
            var provider = serviceProvider.GetRequiredService<ModuleManifestProvider>();
            SetupPackageManifestFile(out var manifestPath, out var existing);

            // Act
            await provider.LoadPackageManifest(_logger);

            // Assert
            var manifest = provider.GetManifest();
            manifest.Should().BeEquivalentTo(existing);

            _fileSystem.File.Exists(manifestPath).Should().BeTrue();
        }

        [Fact]
        public async Task Should_return_empty_manifest_on_failure()
        {
            // Arrange
            await using var serviceProvider = SetupServiceProvider();
            var provider = serviceProvider.GetRequiredService<ModuleManifestProvider>();
            var rooted = _fileSystem.GetModuleVersionsFilePath(_instanceOptions);

            // Act
            await provider.LoadPackageManifest(_logger);

            // Assert
            var manifest = provider.GetManifest();
            manifest.Packages.Should().BeEmpty();

            _fileSystem.File.Exists(rooted).Should().BeFalse();
        }
    }

    public class UpdateManifestPackages : ModuleManifestProviderTests
    {
        [Fact]
        public async Task Should_update_manifest_json()
        {
            // Arrange
            await using var serviceProvider = SetupServiceProvider();
            var provider = serviceProvider.GetRequiredService<ModuleManifestProvider>();
            SetupPackageManifestFile(out var manifestPath, out var existing);

            var versionA = "0.5.1";
            var versionB = "2.3.0";
            var versionC = "5.2.3";
            var update = new ModulePackageManifest
            {
                Packages = [
                    new ModuleDependencyPackage { Name = ModuleA, Version = versionA },
                    new ModuleDependencyPackage { Name = ModuleB, Version = versionB },
                    new ModuleDependencyPackage { Name = ModuleC, Version = versionC },
                ]
            };
            await provider.LoadPackageManifest(_logger);

            // Act
            await provider.UpdateManifestPackages(update.Packages);

            // Assert
            var manifest = provider.GetManifest();
            manifest.Packages.Should().HaveCount(3, $"{ModuleA} and {ModuleB} and {ModuleC}");
            manifest.Packages.Should().ContainSingle(k => k.Name == ModuleA && k.Version == versionA);
            manifest.Packages.Should().ContainSingle(k => k.Name == ModuleB && k.Version == versionB);
            manifest.Packages.Should().ContainSingle(k => k.Name == ModuleC && k.Version == versionC);

            _fileSystem.File.Exists(manifestPath).Should().BeTrue();
        }
    }


    [Fact]
    public async Task Default_modules_json_should_be_readable_from_resources()
    {
        var assembly = Assembly.GetAssembly(typeof(Program));

        using var stream = assembly!.GetManifestResourceStream(ModuleConstants.InitialModulesJsonResourceKey);
        Assert.NotNull(stream);

        var manifest = await JsonSerializer.DeserializeAsync<ModulePackageManifest>(stream);
        Assert.NotNull(manifest);
    }
}
