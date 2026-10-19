using Blazor.Shared.Module;
using Blazor.Shared.Module.ControlPanels.Services;
using Blazor.Shared.Module.Models;
using Blazor.Shared.Module.Services;
using Core.Shared.Modules.Contracts;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NSubstitute.ExceptionExtensions;
using Sdk.Client.ControlPanels.Services;
using Sdk.Modules;

namespace Blazor.Shared.Tests.Module.ControlPanels.Services;

public sealed class ModuleManagementBrowseControlPanelResetHandlerTests
{
    private readonly IModuleManagementService _mgmtService = Substitute.For<IModuleManagementService>();

    private readonly ModuleDependencyPackage _package = new()
    {
        Name = "ViciOne.TestPackage",
        Version = "1.4.0"
    };

    private ServiceProvider SetupServiceProvider()
        => new ServiceCollection()
            .AddSingleton(_mgmtService)
            .AddSingleton(Substitute.For<ILogger<ModuleManagementBrowseControlPanelResetHandler>>())
            .AddScoped<IControlPanelResetHandler<ModuleManagementBrowseControlPanelState>, ModuleManagementBrowseControlPanelResetHandler>()
            .BuildServiceProvider();

    private ModuleMetadataBundle CreateAvailableBundle() => new()
    {
        ModuleId = _package.Name,
        Metadata = new ModuleMetadata() { MinSuiteSdkVersion = "1.0.0", Name = _package.Name, Version = _package.Version },
        Installed = false
    };

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Should_set_available_modules_and_allow_installation(bool allowInstallation)
    {
        // Arrange
        await using var serviceProvider = SetupServiceProvider();
        using var state = new ModuleManagementBrowseControlPanelState(_mgmtService);
        state.EnqueuedOperations.Add(new ModulePackageOperation(_package, ModulePackageOperationKind.Install));

        _mgmtService.GetAvailableMetadata(false, Arg.Any<CancellationToken>())
            .Returns(new ModuleMetadataResult(ModuleMetadataModelFactory.CreateModels([CreateAvailableBundle()]), allowInstallation));

        var resetHandler = serviceProvider.GetRequiredService<IControlPanelResetHandler<ModuleManagementBrowseControlPanelState>>();

        // Act
        await resetHandler.Reset(state, TestContext.Current.CancellationToken);

        // Assert
        state.Modules.Should().ContainSingle(k => k.Name == _package.Name);
        state.AllowInstallation.Should().Be(allowInstallation);
        state.EnqueuedOperations.Should().BeEmpty();
        await _mgmtService.DidNotReceiveWithAnyArgs().GetInstalledMetadata(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Should_set_request_error_if_loading_fails()
    {
        // Arrange
        await using var serviceProvider = SetupServiceProvider();
        using var state = new ModuleManagementBrowseControlPanelState(_mgmtService);

        _mgmtService.GetAvailableMetadata(false, Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("Repository unreachable"));

        var resetHandler = serviceProvider.GetRequiredService<IControlPanelResetHandler<ModuleManagementBrowseControlPanelState>>();

        // Act
        await resetHandler.Reset(state, TestContext.Current.CancellationToken);

        // Assert
        state.RequestErrorCode.Should().NotBe(0);
        state.RequestErrorMessage.Should().NotBeNullOrEmpty();
        state.IsLoading.Should().BeFalse();
    }

    [Fact]
    public async Task Should_clear_a_previous_request_error()
    {
        // Arrange
        await using var serviceProvider = SetupServiceProvider();
        using var state = new ModuleManagementBrowseControlPanelState(_mgmtService)
        {
            RequestErrorCode = 1,
            RequestErrorMessage = "Failed"
        };

        _mgmtService.GetAvailableMetadata(false, Arg.Any<CancellationToken>()).Returns(new ModuleMetadataResult([], true));

        var resetHandler = serviceProvider.GetRequiredService<IControlPanelResetHandler<ModuleManagementBrowseControlPanelState>>();

        // Act
        await resetHandler.Reset(state, TestContext.Current.CancellationToken);

        // Assert
        state.RequestErrorCode.Should().Be(0);
        state.RequestErrorMessage.Should().BeNull();
    }
}
