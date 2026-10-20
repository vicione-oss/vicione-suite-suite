using Blazor.Shared.Module.ControlPanels;
using Blazor.Shared.Module.ControlPanels.Extensions;
using Blazor.Shared.Module.ControlPanels.Services;
using Blazor.Shared.Module.Models;
using Blazor.Shared.Module.Services;
using Blazor.Shared.Services;
using Blazor.Shared.Settings.Extensions;
using Blazor.Tests.Tools;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Sdk.Client.ControlPanels.Services;

namespace Blazor.Shared.Tests.Module.ControlPanels;

public sealed class ModuleManagementInstalledControlPanelTests
{
    private readonly IModuleManagementService _moduleManagementService = Substitute.For<IModuleManagementService>();

    private static BunitContext SetupTestContext(IModuleManagementService moduleManagementService)
    {
        var ctx = new BunitContext();

        ctx.SetupBlazorUiComponents(setup =>
        {
            setup.Services.AddScoped(_ => Substitute.For<ISuiteControlService>())
                .AddScoped(_ => moduleManagementService)
                .AddControlPanelInfrastructure()
                .AddModuleManagementInstalledControlPanel();
        });

        return ctx;
    }

    [Fact]
    public async Task Should_render_component()
    {
        // Arrange
        using var state = new ModuleManagementInstalledControlPanelState(_moduleManagementService);

        await using var ctx = SetupTestContext(_moduleManagementService);

        _moduleManagementService
            .GetInstalledMetadata(Arg.Any<CancellationToken>())
            .Returns(new ModuleMetadataResult([], true));

        var registry = ctx.Services.GetRequiredService<IControlPanelRegistry<SharedClientModule>>();
        var registryItem = registry.First();

        // Act
        var component = ctx.Render<ModuleManagementInstalledControlPanel>(builder => builder
            .Add(c => c.State, state)
            .AddCascadingValue(registryItem));

        // Assert
        component.Should().NotBeNull();
    }
}
