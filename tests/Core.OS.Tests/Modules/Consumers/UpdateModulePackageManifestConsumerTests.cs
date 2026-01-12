using Core.OS.Instance;
using Core.OS.Modules;
using Core.OS.Modules.Consumers;
using Core.OS.Tests.Extensions;
using Core.Shared.Modules.Commands;
using Core.Shared.Modules.Events;
using AwesomeAssertions;
using MassTransit;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Sdk.Modules;
using Sdk.Testing.Backend;
using TestModule.Backend;
using Xunit;

namespace Core.OS.Tests.Modules.Consumers;

public class UpdateModulePackageManifestConsumerTests
{
    private const string PreReleaseVersion = "ci-393434";
    private const string TestBackendVersion = "0.10.3";

    private readonly IModuleManifestProvider _manifestProvider = Substitute.For<IModuleManifestProvider>();
    private readonly ILocalInstanceInformationProvider _instanceInformation = Substitute.For<ILocalInstanceInformationProvider>();
    private readonly IModuleHost _moduleManager = Substitute.For<IModuleHost>();
    private readonly IModuleMigrator _moduleMigrator = Substitute.For<IModuleMigrator>();
    private readonly Action<IBusRegistrationConfigurator> _configureServices;

    private readonly ModulePackageManifest _packageManifest = new()
    {
        Packages = [
            new()
            {
                Name = TestBackendModule.GetAssemblyName(),
                Version = TestBackendVersion,
            },
            new()
            {
                Name = "ViciOne.Suite.ModuleB",
                Version = "1.8.0",
            },
            new()
            {
                Name = "ViciOne.Suite.ModuleC",
                Version = PreReleaseVersion,
            },
        ]
    };

    public UpdateModulePackageManifestConsumerTests()
    {
        _instanceInformation.SetupLocalInstanceInformation();

        _configureServices = cfg =>
        {
            cfg.AddSingleton(_manifestProvider);
            cfg.AddSingleton(_instanceInformation);
            cfg.AddSingleton(_moduleManager);
            cfg.AddSingleton(_moduleMigrator);
            cfg.AddConsumer<UpdateModulePackageManifestConsumer>();
        };
    }

    [Fact]
    public async Task Should_update_manifest()
    {
        // Arrange
        await using var tester = new MassTransitTester(_configureServices);
        var command = new UpdateModulePackageManifest(_packageManifest);

        _moduleManager.GetContext().Returns(TestFactory.CreateEmptySuiteContext());

        // Act
        await tester.TestCommand<UpdateModulePackageManifest, UpdateModulePackageManifestConsumer>(command);

        // Assert
        await _manifestProvider.Received()
            .UpdateManifestPackages(Arg.Any<List<ModuleDependencyPackage>>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Should_send_updated_event_after_manifest_update()
    {
        // Arrange
        await using var tester = new MassTransitTester(_configureServices);
        var command = new UpdateModulePackageManifest(_packageManifest);

        _moduleManager.GetContext().Returns(TestFactory.CreateEmptySuiteContext());

        // Act
        var updateEvent = await tester.TestCommand<UpdateModulePackageManifest, UpdateModulePackageManifestConsumer, ModuleManifestUpdatedEvent>(command);

        // Assert
        updateEvent.InstanceId.Should().Be(_instanceInformation.Local.Id);
        updateEvent.Success.Should().Be(true);
    }

    [Fact]
    public async Task Should_send_updated_event_after_manifest_update_failed()
    {
        // Arrange
        await using var tester = new MassTransitTester(_configureServices);
        var command = new UpdateModulePackageManifest(_packageManifest);

        // Act
        var updateEvent = await tester.TestCommand<UpdateModulePackageManifest, UpdateModulePackageManifestConsumer, ModuleManifestUpdatedEvent>(command);

        // Assert
        updateEvent.InstanceId.Should().Be(_instanceInformation.Local.Id);
        updateEvent.Success.Should().Be(false);
    }

    [Fact]
    public async Task Should_not_modify_installed_module_on_debug()
    {
        // Arrange
        await using var tester = new MassTransitTester(_configureServices);
        var command = new UpdateModulePackageManifest(_packageManifest);
        var context = TestFactory.CreateEmptySuiteContext();
        var moduleContext = TestFactory.CreateModuleContext(typeof(TestBackendModule), ModuleType.Backend, true);

        context.Modules.Add(moduleContext);

        _moduleManager.GetContext().Returns(context);
        _manifestProvider.GetManifest().Returns(_packageManifest);

        // Act
        var updateEvent = await tester.TestCommand<UpdateModulePackageManifest, UpdateModulePackageManifestConsumer, ModuleManifestUpdatedEvent>(command);

        // Assert
        updateEvent.InstanceId.Should().Be(_instanceInformation.Local.Id);
        updateEvent.Success.Should().Be(true);

        await _manifestProvider.Received().UpdateManifestPackages(
            Arg.Is<List<ModuleDependencyPackage>>(s => s.Count == 3 && s.First(k => k.Name == TestBackendModule.GetAssemblyName()).Version == TestBackendVersion), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Should_install_module_on_debug_with_selected_version()
    {
        // Arrange
        await using var tester = new MassTransitTester(_configureServices);
        var command = new UpdateModulePackageManifest(_packageManifest);
        var context = TestFactory.CreateEmptySuiteContext();
        var moduleContext = TestFactory.CreateModuleContext(typeof(TestBackendModule), ModuleType.Backend, true);

        context.Modules.Add(moduleContext);
        _moduleManager.GetContext().Returns(context);

        var storedManifest = new ModulePackageManifest
        {
            Packages = [.. _packageManifest.Packages.Where(k => k.Name != TestBackendModule.GetAssemblyName())],
            Name = "test",
        };
        _manifestProvider.GetManifest().Returns(storedManifest);

        // Act
        var updateEvent = await tester.TestCommand<UpdateModulePackageManifest, UpdateModulePackageManifestConsumer, ModuleManifestUpdatedEvent>(command);

        // Assert
        updateEvent.InstanceId.Should().Be(_instanceInformation.Local.Id);
        updateEvent.Success.Should().Be(true);

        await _manifestProvider.Received().UpdateManifestPackages(
            Arg.Is<List<ModuleDependencyPackage>>(s => s.Count == 3 && s.First(k => k.Name == TestBackendModule.GetAssemblyName()).Version == TestBackendVersion), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Should_not_ignore_modules_with_versions_to_be_resolved()
    {
        // Arrange
        await using var tester = new MassTransitTester(_configureServices);
        var command = new UpdateModulePackageManifest(_packageManifest);
        var context = TestFactory.CreateEmptySuiteContext();
        var moduleContext = TestFactory.CreateModuleContext(typeof(TestBackendModule), ModuleType.Backend, true);
        var toBeResolved = "ToBeResolved";

        context.Modules.Add(moduleContext);

        _moduleManager.GetContext().Returns(context);
        _manifestProvider.GetManifest().Returns(_packageManifest);

        _packageManifest.Packages.Add(new() { Name = toBeResolved, Version = ModuleConstants.LatestVersionKey});

        // Act
        var updateEvent = await tester.TestCommand<UpdateModulePackageManifest, UpdateModulePackageManifestConsumer, ModuleManifestUpdatedEvent>(command);

        // Assert
        updateEvent.InstanceId.Should().Be(_instanceInformation.Local.Id);
        updateEvent.Success.Should().Be(true);

        await _manifestProvider.Received().UpdateManifestPackages(
            Arg.Is<List<ModuleDependencyPackage>>(s => s.Count == 4 && s.Last(k => k.Name == toBeResolved).Version == ModuleConstants.LatestVersionKey), Arg.Any<CancellationToken>());
    }
}
