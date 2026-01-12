using System.IO.Abstractions;
using System.IO.Abstractions.TestingHelpers;
using AwesomeAssertions;
using Core.Module;
using Core.Module.Contracts;
using Core.Module.Options;
using Core.Module.Utils;
using Core.OS.Extensions;
using Core.OS.Hosting;
using Core.OS.Modules;
using Core.OS.Modules.Services;
using Core.Shared.Modules;
using NSubstitute;
using Sdk.Modules;
using Sdk.Testing.Backend;
using Xunit;

namespace Core.OS.Tests.Modules.Services;

public class ModuleSynchronizerTests
{
    private const string ModulesFolder = "/opt/app/suite-modules";
    private const string ModuleA = "Module.A";
    private const string ModuleB = "Module.B";

    private readonly IFileSystem _fileSystem = Substitute.For<IFileSystem>();
    private readonly IModuleApiAdapter _moduleApi = Substitute.For<IModuleApiAdapter>();
    private readonly ModuleLoaderOptions _loaderOptions = new TestConfig()
                .AddModuleLoaderOptions(modulesPath: ModulesFolder)
                .BuildConfiguration()
                .GetModuleLoaderOptions();

    protected ModuleSynchronizerTests()
    {
        _fileSystem.Path.IsPathRooted(_loaderOptions.ModulesPath).Returns(true);
        _fileSystem.Path.IsPathFullyQualified(_loaderOptions.ModulesPath!).Returns(true);
        _fileSystem.Path.GetFullPath(_loaderOptions.ModulesPath!).Returns(ModulesFolder);
        _fileSystem.Path.Exists(ModulesFolder).Returns(true);
    }

    public sealed class WithApiAdapter : ModuleSynchronizerTests
    {
        private readonly ModuleDependencyPackage _moduleA = new() { Name = ModuleA, Version = ModuleConstants.LatestVersionKey };
        private readonly ModuleDependencyPackage _moduleB = new()
        {
            Name = ModuleB,
            Version = "1.2.0",
            DependingOn = [new() { Name = ModuleA, Version = ModuleConstants.LatestVersionKey },]
        };

        public WithApiAdapter()
        {
            _fileSystem.Path.Exists(ModulesFolder).Returns(true);
        }

        [Fact]
        public async Task Should_use_given_api_adapter()
        {
            // Arrange
            using var synchronizer = new ModuleSynchronizer(_fileSystem);
            synchronizer
                .WithPackages([])
                .WithApiAdapter(_moduleApi)
                .WithModulesPath(ModulesFolder);

            // Act 
            var result = await synchronizer.ProcessSynchronization();

            // Assert
            result.All.Should().BeEmpty();
        }

        [Fact]
        public async Task Should_create_api_adapter_from_options()
        {
            // Arrange            
            using var synchronizer = new ModuleSynchronizer(_fileSystem);
            synchronizer
                .WithPackages([])
                .WithApiAdapter(new ModuleApiOptions() { Endpoint = "http://vicione-test.ifm.com" })
                .WithModulesPath(ModulesFolder);

            // Act + Assert
            await synchronizer.ProcessSynchronization();
        }

        [Fact]
        public void Should_throw_if_adapter_is_added()
        {
            // Arrange            
            using var synchronizer = new ModuleSynchronizer(_fileSystem);
            synchronizer
                .WithPackages([])
                .WithModulesPath(ModulesFolder);

            // Act
            var action = () => synchronizer.ProcessSynchronization();

            // Assert
            action.Should().ThrowAsync<InvalidOperationException>();
        }

        [Fact]
        public void Should_throw_if_both_methods_are_used()
        {
            // Arrange            
            using var synchronizer = new ModuleSynchronizer(_fileSystem);
            synchronizer
                .WithPackages([])
                .WithApiAdapter(_moduleApi)
                .WithApiAdapter(new ModuleApiOptions())
                .WithModulesPath(ModulesFolder);

            // Act
            var action = () => synchronizer.ProcessSynchronization();

            // Assert
            action.Should().ThrowAsync<InvalidOperationException>();
        }

        [Fact]
        public async Task Should_resolve_versions_with_latest_version_key()
        {
            // Arrange
            ModuleDependencyPackage _moduleC = new()
            {
                Name = "Module.C",
                Version = "latest",
                DependingOn = [_moduleA,]
            };

            using var synchronizer = new ModuleSynchronizer(_fileSystem);
            synchronizer
                .WithPackages([_moduleB, _moduleC])
                .WithApiAdapter(_moduleApi)
                .WithModulesPath(ModulesFolder);

            var latestVersion = "1.24.0";

            var response = new ModuleArtifactInfo()
            {
                Name = $"{latestVersion}-win-x64_0.19.0.json",
                Path = $"modules/{_moduleC}/",
                Repo = "vicione-suite"
            };

            _moduleApi.QueryLatestModuleMetadataArtifact(Arg.Any<Version>(), _moduleC.Name, false, Arg.Any<CancellationToken>())
                .Returns(response);

            // Act
            var result = await synchronizer.ProcessSynchronization();

            // Assert
            result.Resolved.Should().HaveCount(1, "Module.C -> 1.24.0");
            result.Resolved.Should().ContainSingle(k => k.Name == _moduleC.Name);
        }

        [Fact]
        public async Task Should_try_download_and_extract_resolved_module_versions()
        {
            // Arrange
            using var synchronizer = new ModuleSynchronizer(_fileSystem);
            synchronizer
                .WithPackages([_moduleA, _moduleB])
                .WithApiAdapter(_moduleApi)
                .WithModulesPath(ModulesFolder);

            // Act
            var result = await synchronizer.ProcessSynchronization();

            // Assert
            await _moduleApi.Received().DownloadAndExtract(
                Arg.Any<string>(),
                Arg.Is<ModuleDependencyPackage[]>(p => p.Contains(_moduleB) && p.Length == 1),
                Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task Should_not_download_and_extract_unresolved_module_versions()
        {
            // Arrange
            using var synchronizer = new ModuleSynchronizer(_fileSystem);
            synchronizer
                .WithPackages([_moduleA])
                .WithApiAdapter(_moduleApi)
                .WithModulesPath(ModulesFolder);

            // Act
            var result = await synchronizer.ProcessSynchronization();

            // Assert
            await _moduleApi.DidNotReceive().DownloadAndExtract(
                Arg.Any<string>(),
                Arg.Any<ModuleDependencyPackage[]>(),
                Arg.Any<CancellationToken>());
        }
    }

    public sealed class WithPackageSdkValidation : ModuleSynchronizerTests
    {
        private readonly ModuleDependencyPackage _moduleA = new() { Name = ModuleA, Version = ModuleConstants.LatestVersionKey };
        private readonly ModuleDependencyPackage _moduleB = new()
        {
            Name = ModuleB,
            Version = "1.2.0",
            DependingOn = [new() { Name = ModuleA, Version = ModuleConstants.LatestVersionKey },]
        };

        public WithPackageSdkValidation()
        {
            _fileSystem.Path.Exists(ModulesFolder).Returns(true);
        }

        [Fact]
        public async Task Should_use_given_api_adapter()
        {
            // Arrange
            using var synchronizer = new ModuleSynchronizer(_fileSystem);
            synchronizer
                .WithPackageSdkValidation()
                .WithPackages([_moduleA, _moduleB])
                .WithApiAdapter(_moduleApi)
                .WithModulesPath(ModulesFolder);

            // Act 
            var result = await synchronizer.ProcessSynchronization();

            // Assert
            result.Incompatible.Should().ContainSingle(k => k.Name == ModuleB
                && k.Error!.ErrorCode == ModuleErrorCodes.UnknownCompatibilityError, "Only B validated because A has no version");
        }


        [Fact]
        public async Task Should_attempt_validation_by_local_module_metadata()
        {
            // Arrange
            var fileSystem = new MockFileSystem();
            var sdkVersion = SuiteVersionUtils.GetSuiteSdkVersion();
            fileSystem.AddDirectory(ModulesFolder);
            AddModuleDirectory(fileSystem, _moduleB);

            var moduleMetadata = new ModuleMetadata()
            {
                Name = _moduleB.Name,
                Version = _moduleB.Version,
                MinSuiteSdkVersion = sdkVersion
            };

            var moduleFolder = fileSystem.Path.Combine(fileSystem.Path.GetFullPath(ModulesFolder), moduleMetadata.Name, moduleMetadata.Version);
            fileSystem.SetupModuleMetadataJson(moduleMetadata, moduleFolder);

            using var synchronizer = new ModuleSynchronizer(fileSystem);
            synchronizer
                .WithPackageSdkValidation()
                .WithPackages([_moduleA, _moduleB])
                .WithApiAdapter(_moduleApi)
                .WithModulesPath(ModulesFolder);

            // Act 
            var result = await synchronizer.ProcessSynchronization();

            // Assert
            result.Incompatible.Should().BeEmpty();
        }

        [Fact]
        public async Task Should_use_remote_module_metadata_if_local_is_not_available()
        {
            // Arrange
            var moduleMetadata = new ModuleMetadata()
            {
                Name = _moduleB.Name,
                Version = _moduleB.Version,
                MinSuiteSdkVersion = SuiteVersionUtils.GetSuiteSdkVersion()
            };

            _moduleApi.GetModuleMetadata(_moduleB, Arg.Any<CancellationToken>())
                .Returns(moduleMetadata);

            using var synchronizer = new ModuleSynchronizer(_fileSystem);
            synchronizer
                .WithPackageSdkValidation()
                .WithPackages([_moduleA, _moduleB])
                .WithApiAdapter(_moduleApi)
                .WithModulesPath(ModulesFolder);

            // Act 
            var result = await synchronizer.ProcessSynchronization();

            // Assert
            result.Incompatible.Should().BeEmpty();
        }

        [Fact]
        public async Task Should_try_to_resolve_incompatible_modules_to_latest()
        {
            // Arrange
            var moduleMetadata = new ModuleMetadata()
            {
                Name = _moduleB.Name,
                Version = _moduleB.Version,
                MinSuiteSdkVersion = "0.43.0"
            };

            _moduleApi.GetModuleMetadata(_moduleB, Arg.Any<CancellationToken>())
                .Returns(moduleMetadata);

            using var synchronizer = new ModuleSynchronizer(_fileSystem);
            synchronizer
                .WithPackageSdkValidation()
                .WithPackages([_moduleA, _moduleB])
                .WithApiAdapter(_moduleApi)
                .WithModulesPath(ModulesFolder);

            // Act 
            var result = await synchronizer.ProcessSynchronization();

            // Assert
            result.Incompatible.Should().ContainSingle(k => k.Name == ModuleB
                && k.Error!.ErrorCode == ModuleErrorCodes.SdkVersionIncompatible);

            result.Resolved.Should().ContainSingle(k => k.Name == ModuleB
                && k.Error!.ErrorCode == ModuleErrorCodes.FoundNoVersion);
        }

        [Fact]
        public async Task Should_not_resolve_module_to_ci_version()
        {
            // Arrange
            var moduleArtifact = new ModuleArtifactInfo()
            {
                Name = "0.24.0-ci2343243-linux-x64.zip",
                Path = $"/modules/{_moduleA.Name}",
                Repo = "vicione"
            };

            _moduleApi.QueryLatestModuleMetadataArtifact(ModuleHelpers.GetSdkAssemblyVersion(), _moduleA.Name, false, Arg.Any<CancellationToken>())
                .Returns(moduleArtifact);

            using var synchronizer = new ModuleSynchronizer(_fileSystem);
            synchronizer
                .WithPackageSdkValidation()
                .WithPackages([_moduleA])
                .WithApiAdapter(_moduleApi)
                .WithModulesPath(ModulesFolder);

            // Act 
            var result = await synchronizer.ProcessSynchronization();

            // Assert
            result.Resolved.Should().ContainSingle(k => k.Name == ModuleA
                && k.Error!.ErrorCode == ModuleErrorCodes.FoundCiVersionOnly);
        }

        [Fact]
        public async Task Should_not_resolve_module_to_invalid_version()
        {
            // Arrange
            var moduleArtifact = new ModuleArtifactInfo()
            {
                Name = "a.b.c-123412-linux-x64.zip",
                Path = $"/modules/{_moduleA.Name}",
                Repo = "vicione"
            };

            _moduleApi.QueryLatestModuleMetadataArtifact(ModuleHelpers.GetSdkAssemblyVersion(), _moduleA.Name, false, Arg.Any<CancellationToken>())
                .Returns(moduleArtifact);

            using var synchronizer = new ModuleSynchronizer(_fileSystem);
            synchronizer
                .WithPackageSdkValidation()
                .WithPackages([_moduleA, _moduleB])
                .WithApiAdapter(_moduleApi)
                .WithModulesPath(ModulesFolder);

            // Act 
            var result = await synchronizer.ProcessSynchronization();

            // Assert
            result.Resolved.Should().ContainSingle(k => k.Name == ModuleA
                && k.Error!.ErrorCode == ModuleErrorCodes.FoundInvalidVersion);
        }
    }

    public sealed class WithModulesPath : ModuleSynchronizerTests
    {
        [Fact]
        public async Task Should_use_existing_modules_path()
        {
            // Arrange
            using var synchronizer = new ModuleSynchronizer(_fileSystem);
            synchronizer
                .WithPackages([])
                .WithApiAdapter(_moduleApi)
                .WithModulesPath(ModulesFolder);

            _fileSystem.Path.Exists(ModulesFolder).Returns(true);

            // Act 
            var result = await synchronizer.ProcessSynchronization();

            // Assert
            result.All.Should().BeEmpty();
        }

        [Fact]
        public void Should_throw_on_not_existing_modules_path()
        {
            // Arrange
            using var synchronizer = new ModuleSynchronizer(_fileSystem);
            synchronizer
                .WithPackages([])
                .WithApiAdapter(_moduleApi)
                .WithModulesPath(ModulesFolder);

            // Act
            var action = () => synchronizer.ProcessSynchronization();

            // Assert
            action.Should().ThrowAsync<DirectoryNotFoundException>();
        }

        [Fact]
        public async Task Should_create_modules_path()
        {
            // Arrange            
            using var synchronizer = new ModuleSynchronizer(_fileSystem);
            synchronizer
                .WithPackages([])
                .WithApiAdapter(_moduleApi)
                .WithModulesPath(_loaderOptions);

            // Act
            await synchronizer.ProcessSynchronization();

            // Assert
            _fileSystem.Directory.Received().CreateDirectory(ModulesFolder);
        }

        [Fact]
        public void Should_throw_if_both_methods_are_used()
        {
            // Arrange            
            using var synchronizer = new ModuleSynchronizer(_fileSystem);
            synchronizer
                .WithPackages([])
                .WithApiAdapter(_moduleApi)
                .WithModulesPath(_loaderOptions)
                .WithModulesPath(ModulesFolder);

            // Act
            var action = () => synchronizer.ProcessSynchronization();

            // Assert
            action.Should().ThrowAsync<InvalidOperationException>();
        }
    }

    public sealed class WithOrphanedVersionCleanup : ModuleSynchronizerTests
    {
        private readonly ModuleDependencyPackage _moduleA = new() { Name = ModuleA, Version = "0.3.0" };
        private readonly ModuleDependencyPackage _moduleB = new()
        {
            Name = ModuleB,
            Version = "1.2.0",
            DependingOn = [new() { Name = ModuleA, Version = "0.3.0" },]
        };

        private readonly ModuleDependencyPackage _orphanedModuleC = new() { Name = "Orphaned", Version = "1.1.7" };
        private readonly ModuleDependencyPackage _orphanedModuleD = new() { Name = "Orphaned", Version = "2.4.2" };
        private readonly ModuleDependencyPackage _orphanedModuleE = new() { Name = "Orphaned", Version = "2.4.2-ci234234" };

        [Fact]
        public async Task Should_only_delete_orphaned_module_folders()
        {
            // Arrange            
            var fileSystem = new MockFileSystem();
            AddModuleDirectory(fileSystem, _moduleA);
            AddModuleDirectory(fileSystem, _moduleB);
            AddModuleDirectory(fileSystem, _orphanedModuleC);
            AddModuleDirectory(fileSystem, _orphanedModuleD);
            AddModuleDirectory(fileSystem, _orphanedModuleE);

            using var synchronizer = new ModuleSynchronizer(fileSystem);
            synchronizer
                .WithApiAdapter(_moduleApi)
                .WithPackages([_moduleA, _moduleB])
                .WithModulesPath(ModulesFolder)
                .WithOrphanedVersionCleanup();

            // Act
            await synchronizer.ProcessSynchronization();

            // Assert            
            fileSystem.Directory.Exists(GetModuleDirectory(fileSystem, _moduleA)).Should().BeTrue();
            fileSystem.Directory.Exists(GetModuleDirectory(fileSystem, _moduleB)).Should().BeTrue();
            fileSystem.Directory.Exists(GetModuleDirectory(fileSystem, _orphanedModuleC)).Should().BeFalse();
            fileSystem.Directory.Exists(GetModuleDirectory(fileSystem, _orphanedModuleD)).Should().BeFalse();
            fileSystem.Directory.Exists(GetModuleDirectory(fileSystem, _orphanedModuleE)).Should().BeFalse();
        }

        [Fact]
        public async Task Should_not_delete_internal_module_folders()
        {
            // Arrange
            var fileSystem = new MockFileSystem();

            var clusterMgmtPath = fileSystem.Path.Combine(ModulesFolder, "ViciOne.Suite.ClusterManagement", "wwwroot");
            var blazorServerPath1 = fileSystem.Path.Combine(ModulesFolder, "ViciOne.Suite.Blazor.Server", "de");
            var blazorServerPath2 = fileSystem.Path.Combine(ModulesFolder, "ViciOne.Suite.Blazor.Server", "AppData");

            fileSystem.AddDirectory(clusterMgmtPath);
            fileSystem.AddDirectory(blazorServerPath1);
            fileSystem.AddDirectory(blazorServerPath2);

            using var synchronizer = new ModuleSynchronizer(fileSystem);
            synchronizer
                .WithPackages([])
                .WithApiAdapter(_moduleApi)
                .WithModulesPath(ModulesFolder)
                .WithOrphanedVersionCleanup();

            // Act
            await synchronizer.ProcessSynchronization();

            // Assert            
            fileSystem.Directory.Exists(clusterMgmtPath).Should().BeTrue();
            fileSystem.Directory.Exists(blazorServerPath1).Should().BeTrue();
            fileSystem.Directory.Exists(blazorServerPath2).Should().BeTrue();
        }

        [Fact]
        public async Task Should_not_delete_orphaned_module_folders_if_not_configured()
        {
            // Arrange            
            var fileSystem = new MockFileSystem();
            AddModuleDirectory(fileSystem, _moduleA);
            AddModuleDirectory(fileSystem, _orphanedModuleC);

            using var synchronizer = new ModuleSynchronizer(fileSystem);
            synchronizer
                .WithApiAdapter(_moduleApi)
                .WithPackages([_moduleA, _moduleB])
                .WithModulesPath(ModulesFolder);

            // Act
            await synchronizer.ProcessSynchronization();

            // Assert            
            fileSystem.Directory.Exists(GetModuleDirectory(fileSystem, _moduleA)).Should().BeTrue();
            fileSystem.Directory.Exists(GetModuleDirectory(fileSystem, _orphanedModuleC)).Should().BeTrue();
        }
    }

    private static void AddModuleDirectory(MockFileSystem fileSystem, ModuleDependencyPackage package)
        => fileSystem.AddDirectory(GetModuleDirectory(fileSystem, package));

    private static string GetModuleDirectory(MockFileSystem fileSystem, ModuleDependencyPackage package)
        => fileSystem.Path.Combine(ModulesFolder, package.Name, package.Version);
}
