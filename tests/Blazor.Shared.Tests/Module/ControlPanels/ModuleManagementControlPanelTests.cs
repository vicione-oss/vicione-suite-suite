using Blazor.Shared.Module.ControlPanels;
using Blazor.Shared.Module.ControlPanels.Extensions;
using Blazor.Shared.Module.Services;
using Blazor.Shared.Services;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Sdk.Client.ControlPanels.Services;
using Sdk.Testing.Client;
using Xunit;

namespace Blazor.Shared.Tests.Module.ControlPanels;

public class ModuleManagementControlPanelTests
{
    private readonly IModuleManagementService _moduleManagementService = Substitute.For<IModuleManagementService>();

    private static TestContext SetupTestContext(IModuleManagementService moduleManagementService)
    {
        var ctx = new TestContext();

        ctx.SetupSuiteServices(setup =>
        {
            setup.Services.AddScoped(_ => Substitute.For<IActiveControlPanelDescriptorProvider>())
                .AddScoped(_ => Substitute.For<IActiveControlPanelPageProvider>())
                .AddScoped(_ => Substitute.For<ISuiteControlService>())
                .AddScoped(_ => moduleManagementService)
                .AddModuleManagementControlPanel();
        });

        return ctx;
    }

    [Fact]
    public void Should_render_component()
    {
        // Arrange
        var state = new ModuleManagementControlPanelState();

        using var ctx = SetupTestContext(_moduleManagementService);

        _moduleManagementService.GetModuleMetadata(false, false, Arg.Any<CancellationToken>())
            .Returns(new Core.Shared.Modules.Requests.GetModuleMetadataBundlesResponse([], false));

        // Act
        var component = ctx.RenderComponent<ModuleManagementControlPanel>(p => p.Add(c => c.State, state));

        // Assert
        Assert.NotNull(component);
    }
}
