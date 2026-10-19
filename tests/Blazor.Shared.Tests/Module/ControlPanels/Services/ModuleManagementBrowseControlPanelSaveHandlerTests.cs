using Blazor.Shared.Module.ControlPanels.Models;
using Blazor.Shared.Module.ControlPanels.Services;
using Blazor.Shared.Module.Services;
using Core.Shared.Modules.Contracts;
using Microsoft.Extensions.DependencyInjection;
using Sdk.Client.ControlPanels.Models;
using Sdk.Client.ControlPanels.Services;
using Sdk.Modules;

namespace Blazor.Shared.Tests.Module.ControlPanels.Services;

public sealed class ModuleManagementBrowseControlPanelSaveHandlerTests
{
    private readonly IModuleManagementService _mgmtService = Substitute.For<IModuleManagementService>();

    private readonly ModuleDependencyPackage _package = new()
    {
        Name = "ViciOne.TestPackage",
        Version = "1.4.0"
    };

    private ServiceProvider SetupServiceProvider()
        => new ServiceCollection()
            .AddSingleton(_ => _mgmtService)
            .AddScoped<IControlPanelSaveHandler<ModuleManagementBrowseControlPanelState>, ModuleManagementBrowseControlPanelSaveHandler>()
            .BuildServiceProvider();

    [Fact]
    public async Task Should_update_only_its_own_operations_and_return_success()
    {
        // Arrange
        _mgmtService.UpdateOperations(Arg.Any<List<ModulePackageOperation>>(), Arg.Any<CancellationToken>())
            .Returns(new ModuleManagementServiceSuccessResult());

        await using var serviceProvider = SetupServiceProvider();
        var installOperation = new ModulePackageOperation(_package, ModulePackageOperationKind.Install);
        using var state = new ModuleManagementBrowseControlPanelState(_mgmtService);
        state.EnqueuedOperations.Add(installOperation);

        using var otherTabState = new ModuleManagementInstalledControlPanelState(_mgmtService);
        otherTabState.EnqueuedOperations.Add(new ModulePackageOperation(_package, ModulePackageOperationKind.Uninstall));

        var saveHandler = serviceProvider.GetRequiredService<IControlPanelSaveHandler<ModuleManagementBrowseControlPanelState>>();

        // Act
        var result = await saveHandler.Save(state, TestContext.Current.CancellationToken);

        // Assert
        result.Should().BeOfType<SaveSuccessResult>();
        await _mgmtService.Received(1).UpdateOperations(Arg.Is<List<ModulePackageOperation>>(
            k => k.Count == 1 && k[0] == installOperation), Arg.Any<CancellationToken>());
        state.EnqueuedOperations.Should().BeEmpty();
        otherTabState.EnqueuedOperations.Should().ContainSingle();
    }

    [Fact]
    public async Task Should_keep_operations_when_update_fails()
    {
        // Arrange
        _mgmtService.UpdateOperations(Arg.Any<List<ModulePackageOperation>>(), Arg.Any<CancellationToken>())
            .Returns(new ModuleManagementServiceErrorResult("update failed", 100));

        await using var serviceProvider = SetupServiceProvider();
        using var state = new ModuleManagementBrowseControlPanelState(_mgmtService);
        state.EnqueuedOperations.Add(new ModulePackageOperation(_package, ModulePackageOperationKind.Install));

        var saveHandler = serviceProvider.GetRequiredService<IControlPanelSaveHandler<ModuleManagementBrowseControlPanelState>>();

        // Act
        var result = await saveHandler.Save(state, TestContext.Current.CancellationToken);

        // Assert
        result.Should().BeOfType<SaveErrorResult>();
        state.EnqueuedOperations.Should().ContainSingle();
    }
}
