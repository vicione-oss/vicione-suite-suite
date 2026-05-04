using AwesomeAssertions;
using Blazor.Shared.Module;
using Blazor.Shared.Module.ControlPanels.Services;
using Blazor.Shared.Module.Services;
using Core.Shared.Modules.Contracts;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Sdk.Client.ControlPanels.Services;
using Sdk.Modules;
using Xunit;

namespace Blazor.Shared.Tests.Module.ControlPanels.Services;

public sealed class ModuleManagementControlPanelResetHandlerTests
{
    private readonly IModuleManagementService _mgmtService = Substitute.For<IModuleManagementService>();

    private readonly ModuleDependencyPackage _package = new()
    {
        Name = "ViciOne.TestPackage",
        Version = "1.4.0"
    };

    private readonly ModuleDependencyPackage _anotherPackage = new()
    {
        Name = "ViciOne.OtherPackage",
        Version = "2.1.17"
    };

    private ServiceProvider SetupServiceProvider()
    {
        var services = new ServiceCollection()
            .AddSingleton(_mgmtService)
            .AddScoped<IControlPanelResetHandler<ModuleManagementControlPanelState>, ModuleManagementControlPanelResetHandler>();

        return services.BuildServiceProvider();
    }

    [Fact]
    public async Task Should_reset_pending_operations()
    {
        // Arrange
        await using var serviceProvider = SetupServiceProvider();
        var state = new ModuleManagementControlPanelState();
        state.InstallOperations.Add(new ModulePackageOperation(_package, ModulePackageOperationKind.Install));
        state.UninstallOperations.Add(new ModulePackageOperation(_anotherPackage, ModulePackageOperationKind.Uninstall));

        _mgmtService.GetMetadata(Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns([]);

        var saveHandler = serviceProvider.GetRequiredService<IControlPanelResetHandler<ModuleManagementControlPanelState>>();

        // Act
        await saveHandler.Reset(state, TestContext.Current.CancellationToken);

        // Assert
        state.InstallOperations.Should().BeEmpty();
        state.UninstallOperations.Should().BeEmpty();
    }

    [Fact]
    public async Task Should_set_installed_and_available_modules()
    {
        // Arrange
        await using var serviceProvider = SetupServiceProvider();
        var name = "Available";
        var installedBundle = new ModuleMetadataBundle
        {
            ModuleId = _package.Name,
            Metadata = new ModuleMetadata() { MinSuiteSdkVersion = "1.0.0", Name = _package.Name, Version = "1.2.3" },
            Installed = true
        };

        var anotherBundle = new ModuleMetadataBundle
        {
            ModuleId = _anotherPackage.Name,
            Metadata = new ModuleMetadata() { MinSuiteSdkVersion = "1.0.0", Name = _anotherPackage.Name, Version = "2.4.1" },
            Installed = true
        };

        var availableBundle = new ModuleMetadataBundle
        {
            ModuleId = name,
            Metadata = new ModuleMetadata() { MinSuiteSdkVersion = "1.0.0", Name = name, Version = "6.1.2" },
            Installed = false
        };

        var state = new ModuleManagementControlPanelState();

        _mgmtService.GetMetadata(Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(ModuleMetadataModelFactory.CreateModels(
            [
                installedBundle,
                anotherBundle,
                availableBundle
            ]));

        var saveHandler = serviceProvider.GetRequiredService<IControlPanelResetHandler<ModuleManagementControlPanelState>>();

        // Act
        await saveHandler.Reset(state, TestContext.Current.CancellationToken);

        // Assert
        state.InstalledModules.Should().ContainSingle(k => k.Name == _package.Name);
        state.InstalledModules.Should().ContainSingle(k => k.Name == _anotherPackage.Name);
        state.AvailableModules.Should().ContainSingle(k => k.Name == name);
    }
}
