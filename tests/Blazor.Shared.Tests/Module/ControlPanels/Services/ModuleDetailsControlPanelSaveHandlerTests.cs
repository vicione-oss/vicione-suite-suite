using AwesomeAssertions;
using Blazor.Shared.Module.ControlPanels.Models;
using Blazor.Shared.Module.ControlPanels.Services;
using Blazor.Shared.Module.Models;
using Blazor.Shared.Module.Services;
using Core.Shared.Modules.Contracts;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Sdk.Client.ControlPanels.Models;
using Sdk.Client.ControlPanels.Services;
using Sdk.Client.Services;
using Sdk.Modules;
using Xunit;

namespace Blazor.Shared.Tests.Module.ControlPanels.Services;

public sealed class ModuleDetailsControlPanelSaveHandlerTests
{
    private static readonly ModuleDependencyPackage _package = new()
    {
        Name = "ViciOne.TestPackage",
        Version = "1.4.0"
    };

    private readonly IModuleManagementService _mgmtService = Substitute.For<IModuleManagementService>();
    private readonly IMessageBannerService _bannerService = Substitute.For<IMessageBannerService>();
    private readonly ModuleMetadataBundle _installedBundle = new()
    {
        ModuleId = "ViciOne.TestPackage",
        Metadata = new ModuleMetadata() { MinSuiteSdkVersion = "1.0.0", Name = _package.Name, Version = _package.Version },
        Installed = true,
    };

    private ServiceProvider SetupServiceProvider()
    {
        var services = new ServiceCollection()
            .AddSingleton(_ => _mgmtService)
            .AddSingleton(_ => _bannerService)
            .AddScoped<IControlPanelSaveHandler<ModuleDetailsControlPanelState>, ModuleDetailsControlPanelSaveHandler>();

        return services.BuildServiceProvider();
    }

    [Fact]
    public async Task Should_update_options_with_success_when_modified()
    {
        // Arrange
        await using var serviceProvider = SetupServiceProvider();
        var saveHandler = serviceProvider.GetRequiredService<IControlPanelSaveHandler<ModuleDetailsControlPanelState>>();

        var options = new Dictionary<string, ModuleOptionDeclaration>
        {
            { "option1", new ModuleOptionDeclaration { Key = "option1", Value = "value1", OptionType = ModuleOptionType.Text } },
            { "option2", new ModuleOptionDeclaration { Key = "option2", Value = "true", OptionType = ModuleOptionType.Boolean } }
        };

        var state = new ModuleDetailsControlPanelState()
        {
            ModuleMetadata = new ModuleMetadataModel
            {
                Bundle = _installedBundle,
                Installed = true,
                EditOptions = options,
                HasModifiedOptions = true,
            },
            VersionToInstall = _installedBundle.Metadata.Version    // skip operation updates
        };

        _mgmtService.UpdateOptions(state.ModuleMetadata.ModuleId, Arg.Any<List<ModuleOptionDeclaration>>(), Arg.Any<CancellationToken>())
            .Returns(new ModuleManagementServiceSuccessResult());

        // Act
        var result = await saveHandler.Save(state, TestContext.Current.CancellationToken);

        // Assert
        result.Should().BeOfType<SaveSuccessResult>();
        state.ModuleMetadata.HasModifiedOptions.Should().BeFalse();
        await _mgmtService.Received(1).UpdateOptions(state.ModuleMetadata.ModuleId, Arg.Is<ICollection<ModuleOptionDeclaration>>(opts =>
            opts.Count == 2 &&
            opts.Any(o => o.Key == "option1" && o.Value == "value1" && o.OptionType == ModuleOptionType.Text) &&
            opts.Any(o => o.Key == "option2" && o.Value == "true" && o.OptionType == ModuleOptionType.Boolean)
        ), Arg.Any<CancellationToken>());
    }


    [Fact]
    public async Task Should_return_save_error_if_option_update_fails_and_keep_modified_state()
    {
        // Arrange
        await using var serviceProvider = SetupServiceProvider();
        var saveHandler = serviceProvider.GetRequiredService<IControlPanelSaveHandler<ModuleDetailsControlPanelState>>();

        var options = new Dictionary<string, ModuleOptionDeclaration>
        {
            { "option1", new ModuleOptionDeclaration { Key = "option1", Value = "value1", OptionType = ModuleOptionType.Text } },
            { "option2", new ModuleOptionDeclaration { Key = "option2", Value = "true", OptionType = ModuleOptionType.Boolean } }
        };

        var state = new ModuleDetailsControlPanelState()
        {
            ModuleMetadata = new ModuleMetadataModel
            {
                Bundle = _installedBundle,
                Installed = true,
                EditOptions = options,
                HasModifiedOptions = true,
            },
            VersionToInstall = _installedBundle.Metadata.Version    // skip operation updates
        };

        _mgmtService.UpdateOptions(state.ModuleMetadata.ModuleId, Arg.Any<ICollection<ModuleOptionDeclaration>>(), Arg.Any<CancellationToken>())
            .Returns(new ModuleManagementServiceErrorResult());

        // Act
        var result = await saveHandler.Save(state, TestContext.Current.CancellationToken);

        // Assert
        result.Should().BeOfType<SaveErrorResult>();
        state.ModuleMetadata.HasModifiedOptions.Should().BeTrue();
    }

    [Fact]
    public async Task Should_update_operations_with_success()
    {
        // Arrange
        await using var serviceProvider = SetupServiceProvider();
        var saveHandler = serviceProvider.GetRequiredService<IControlPanelSaveHandler<ModuleDetailsControlPanelState>>();

        var state = new ModuleDetailsControlPanelState()
        {
            ModuleMetadata = new ModuleMetadataModel
            {
                Bundle = _installedBundle,
                EditOptions = [],
                PendingOperation = new ModulePackageOperation(_package, ModulePackageOperationKind.Uninstall)
            },
            VersionToInstall = "1.5.0"
        };

        _mgmtService.UpdateOperations(Arg.Any<List<ModulePackageOperation>>(), Arg.Any<CancellationToken>())
            .Returns(new ModuleManagementServiceSuccessResult());

        // Act
        var result = await saveHandler.Save(state, TestContext.Current.CancellationToken);

        // Assert
        result.Should().BeOfType<SaveSuccessResult>();
        await _mgmtService.Received(1).UpdateOperations(Arg.Is<List<ModulePackageOperation>>(ops =>
            ops.Any(op => op.Package.Name == _package.Name &&
            op.Package.Version == state.VersionToInstall)), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Should_update_operations_with_error_when_service_returns_error()
    {
        // Arrange
        await using var serviceProvider = SetupServiceProvider();
        var saveHandler = serviceProvider.GetRequiredService<IControlPanelSaveHandler<ModuleDetailsControlPanelState>>();

        var state = new ModuleDetailsControlPanelState()
        {
            ModuleMetadata = new ModuleMetadataModel
            {
                Bundle = _installedBundle,
                EditOptions = [],
                PendingOperation = new ModulePackageOperation(_package, ModulePackageOperationKind.Uninstall)
            },
            VersionToInstall = "1.5.0"
        };

        _mgmtService.UpdateOperations(Arg.Any<List<ModulePackageOperation>>(), Arg.Any<CancellationToken>())
            .Returns(new ModuleManagementServiceErrorResult());

        // Act
        var result = await saveHandler.Save(state, TestContext.Current.CancellationToken);

        // Assert
        result.Should().BeOfType<SaveErrorResult>();
        await _mgmtService.Received(1).UpdateOperations(Arg.Is<List<ModulePackageOperation>>(ops =>
            ops.Any(op => op.Package.Name == _package.Name &&
            op.Package.Version == state.VersionToInstall)), Arg.Any<CancellationToken>());
    }
}
