using Blazor.Shared.Module.ControlPanels.Models;
using Blazor.Shared.Module.ControlPanels.Services;
using Blazor.Shared.Module.Services;
using Core.Shared.Modules.Contracts;
using Microsoft.Extensions.DependencyInjection;
using Sdk.Client.ControlPanels.Models;
using Sdk.Client.ControlPanels.Services;
using Sdk.Modules;

namespace Blazor.Shared.Tests.Module.ControlPanels.Services;

public sealed class ModuleManagementInstalledControlPanelSaveHandlerTests
{
    private readonly IModuleManagementService _mgmtService = Substitute.For<IModuleManagementService>();

    private readonly ModuleDependencyPackage _package = new()
    {
        Name = "ViciOne.TestPackage",
        Version = "1.4.0"
    };

    private ServiceProvider SetupServiceProvider()
    {
        var services = new ServiceCollection()
            .AddSingleton(_ => _mgmtService)
            .AddScoped<IControlPanelSaveHandler<ModuleManagementInstalledControlPanelState>, ModuleManagementInstalledControlPanelSaveHandler>();

        return services.BuildServiceProvider();
    }

    [Fact]
    public async Task Should_update_operations_with_success_when_there_are_no_pending_changes()
    {
        // Arrange
        await using var serviceProvider = SetupServiceProvider();
        using var state = new ModuleManagementInstalledControlPanelState(_mgmtService);
        var saveHandler = serviceProvider.GetRequiredService<IControlPanelSaveHandler<ModuleManagementInstalledControlPanelState>>();

        // Act
        var result = await saveHandler.Save(state, TestContext.Current.CancellationToken);

        // Assert
        result.Should().BeOfType<SaveSuccessResult>();
    }

    [Fact]
    public async Task Should_return_error_when_update_fails()
    {
        // Arrange
        _mgmtService.UpdateOperations(Arg.Any<List<ModulePackageOperation>>(), Arg.Any<CancellationToken>())
            .Returns(new ModuleManagementServiceErrorResult("update failed", 100));

        await using var serviceProvider = SetupServiceProvider();
        var operation = new ModulePackageOperation(_package, ModulePackageOperationKind.Uninstall);
        using var state = new ModuleManagementInstalledControlPanelState(_mgmtService);
        state.EnqueuedOperations.Add(operation);

        var saveHandler = serviceProvider.GetRequiredService<IControlPanelSaveHandler<ModuleManagementInstalledControlPanelState>>();

        // Act
        var result = await saveHandler.Save(state, TestContext.Current.CancellationToken);

        // Assert
        var errorResult = result.Should().BeOfType<SaveErrorResult>().Subject;
        errorResult.Message.Should().Be("update failed");
        errorResult.ErrorCode.Should().Be(100);
    }

    [Fact]
    public async Task Should_update_operations_and_return_success()
    {
        // Arrange
        _mgmtService.UpdateOperations(Arg.Any<List<ModulePackageOperation>>(), Arg.Any<CancellationToken>())
            .Returns(new ModuleManagementServiceSuccessResult());

        await using var serviceProvider = SetupServiceProvider();
        var uninstallOperation = new ModulePackageOperation(_package, ModulePackageOperationKind.Uninstall);
        using var state = new ModuleManagementInstalledControlPanelState(_mgmtService);
        state.EnqueuedOperations.Add(uninstallOperation);

        var saveHandler = serviceProvider.GetRequiredService<IControlPanelSaveHandler<ModuleManagementInstalledControlPanelState>>();

        // Act
        var result = await saveHandler.Save(state, TestContext.Current.CancellationToken);

        // Assert
        result.Should().BeOfType<SaveSuccessResult>();
        await _mgmtService.Received(1).UpdateOperations(Arg.Is<List<ModulePackageOperation>>(
            k => k!.Count == 1 && k.Any(op => op == uninstallOperation)), Arg.Any<CancellationToken>());
        state.EnqueuedOperations.Should().BeEmpty();
    }

    [Fact]
    public async Task Should_clear_pending_operations_after_update_success()
    {
        // Arrange
        var uninstallPackage = new ModuleDependencyPackage()
        {
            Name = "ViciOne.OtherPackage",
            Version = "2.1.7"
        };

        _mgmtService.UpdateOperations(Arg.Any<List<ModulePackageOperation>>(), Arg.Any<CancellationToken>())
            .Returns(new ModuleManagementServiceSuccessResult());

        await using var serviceProvider = SetupServiceProvider();
        var uninstallOperation = new ModulePackageOperation(uninstallPackage, ModulePackageOperationKind.Uninstall);
        var installOperation = new ModulePackageOperation(_package, ModulePackageOperationKind.Install);
        using var state = new ModuleManagementInstalledControlPanelState(_mgmtService);
        state.EnqueuedOperations.Add(installOperation);
        state.EnqueuedOperations.Add(uninstallOperation);

        var saveHandler = serviceProvider.GetRequiredService<IControlPanelSaveHandler<ModuleManagementInstalledControlPanelState>>();

        // Act
        var result = await saveHandler.Save(state, TestContext.Current.CancellationToken);

        // Assert
        result.Should().BeOfType<SaveSuccessResult>();
        state.EnqueuedOperations.Should().BeEmpty();
    }

    [Fact]
    public async Task Should_not_call_service_if_we_have_no_changes_and_return_success()
    {
        // Arrange
        await using var serviceProvider = SetupServiceProvider();
        using var state = new ModuleManagementInstalledControlPanelState(_mgmtService);
        var saveHandler = serviceProvider.GetRequiredService<IControlPanelSaveHandler<ModuleManagementInstalledControlPanelState>>();

        // Act
        var result = await saveHandler.Save(state, TestContext.Current.CancellationToken);

        // Assert
        result.Should().BeOfType<SaveSuccessResult>();
        await _mgmtService.Received(0).UpdateOperations(Arg.Any<List<ModulePackageOperation>>(), Arg.Any<CancellationToken>());
    }
}
