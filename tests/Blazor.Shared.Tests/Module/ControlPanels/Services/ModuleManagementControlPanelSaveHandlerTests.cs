using AwesomeAssertions;
using Blazor.Shared.Module.ControlPanels.Models;
using Blazor.Shared.Module.ControlPanels.Services;
using Blazor.Shared.Module.Services;
using Core.Shared.Modules.Contracts;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Sdk.Client.ControlPanels.Models;
using Sdk.Client.ControlPanels.Services;
using Sdk.Modules;
using Xunit;

namespace Blazor.Shared.Tests.Module.ControlPanels.Services;

public sealed class ModuleManagementControlPanelSaveHandlerTests
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
            .AddScoped<IControlPanelSaveHandler<ModuleManagementControlPanelState>, ModuleManagementControlPanelSaveHandler>();

        return services.BuildServiceProvider();
    }

    [Fact]
    public async Task Should_update_operations_with_success_when_there_are_no_pending_changes()
    {
        // Arrange
        await using var serviceProvider = SetupServiceProvider();
        var state = new ModuleManagementControlPanelState();
        var saveHandler = serviceProvider.GetRequiredService<IControlPanelSaveHandler<ModuleManagementControlPanelState>>();

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
        var state = new ModuleManagementControlPanelState();
        state.UninstallOperations.Add(operation);

        var saveHandler = serviceProvider.GetRequiredService<IControlPanelSaveHandler<ModuleManagementControlPanelState>>();

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
        var operation = new ModulePackageOperation(_package, ModulePackageOperationKind.Uninstall);
        var state = new ModuleManagementControlPanelState();
        state.UninstallOperations.Add(operation);

        var saveHandler = serviceProvider.GetRequiredService<IControlPanelSaveHandler<ModuleManagementControlPanelState>>();

        // Act
        var result = await saveHandler.Save(state, TestContext.Current.CancellationToken);

        // Assert
        result.Should().BeOfType<SaveSuccessResult>();
        await _mgmtService.Received(1).UpdateOperations(state.UninstallOperations, Arg.Any<CancellationToken>());
    }
}
