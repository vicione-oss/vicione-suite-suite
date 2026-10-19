using Blazor.Shared.Module;
using Blazor.Shared.Module.ControlPanels.Services;
using Blazor.Shared.Module.Models;
using Blazor.Shared.Module.Services;
using Core.Shared.Modules.Contracts;
using Microsoft.Extensions.DependencyInjection;
using Sdk.Client.ControlPanels.Services;
using Sdk.Modules;

namespace Blazor.Shared.Tests.Module.ControlPanels.Services;

public sealed class ModuleManagementInstalledControlPanelResetHandlerTests
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
        => new ServiceCollection()
            .AddSingleton(_mgmtService)
            .AddScoped<IControlPanelResetHandler<ModuleManagementInstalledControlPanelState>, ModuleManagementInstalledControlPanelResetHandler>()
            .BuildServiceProvider();

    [Fact]
    public async Task Should_reset_pending_operations()
    {
        // Arrange
        await using var serviceProvider = SetupServiceProvider();
        using var state = new ModuleManagementInstalledControlPanelState(_mgmtService);
        state.EnqueuedOperations.Add(new ModulePackageOperation(_package, ModulePackageOperationKind.Uninstall));
        state.EnqueuedOperations.Add(new ModulePackageOperation(_anotherPackage, ModulePackageOperationKind.Uninstall));

        _mgmtService.GetInstalledMetadata(Arg.Any<CancellationToken>()).Returns(new ModuleMetadataResult([], true));

        var resetHandler = serviceProvider.GetRequiredService<IControlPanelResetHandler<ModuleManagementInstalledControlPanelState>>();

        // Act
        await resetHandler.Reset(state, TestContext.Current.CancellationToken);

        // Assert
        state.EnqueuedOperations.Should().BeEmpty();
    }

    [Fact]
    public async Task Should_set_installed_modules()
    {
        // Arrange
        await using var serviceProvider = SetupServiceProvider();
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

        using var state = new ModuleManagementInstalledControlPanelState(_mgmtService);

        _mgmtService.GetInstalledMetadata(Arg.Any<CancellationToken>())
            .Returns(new ModuleMetadataResult(ModuleMetadataModelFactory.CreateModels([installedBundle, anotherBundle]), true));

        var resetHandler = serviceProvider.GetRequiredService<IControlPanelResetHandler<ModuleManagementInstalledControlPanelState>>();

        // Act
        await resetHandler.Reset(state, TestContext.Current.CancellationToken);

        // Assert
        state.Modules.Select(k => k.Name).Should().BeEquivalentTo(_package.Name, _anotherPackage.Name);
        await _mgmtService.DidNotReceiveWithAnyArgs().GetAvailableMetadata(default, TestContext.Current.CancellationToken);
    }
}
