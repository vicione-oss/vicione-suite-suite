using Blazor.Shared.Module.ControlPanels;
using Blazor.Shared.Module.ControlPanels.Services;
using Blazor.Tests.Tools;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Sdk.Modules;
using ViciOne.Ui.Blazor.Components.Grid.Extensions;

namespace Blazor.Shared.Tests.Module.ControlPanels;

public sealed class ModuleOptionDeclarationCollectionGridTests
{
    private static BunitContext SetupTestContext()
    {
        var ctx = new BunitContext();

        ctx.SetupBlazorUiComponents(setup => setup.Services
            .AddGridItemSelectColumn()
            .AddGridItemSelection<string>(typeof(ModuleOptionDeclarationCollectionGridServiceKey), ServiceLifetime.Transient));

        ctx.JSInterop.ConfigureQuickGridJSInterop();

        return ctx;
    }

    private static IRenderedComponent<ModuleOptionDeclarationCollectionGrid> RenderGrid(BunitContext ctx)
        => ctx.Render<ModuleOptionDeclarationCollectionGrid>(builder => builder
            .Add(c => c.Items, new[]
            {
                new ModuleOptionDeclaration { Key = "PackagePassword", Value = "Secret123", OptionType = ModuleOptionType.Text, IsPassword = true },
                new ModuleOptionDeclaration { Key = "PackageUser", Value = "admin", OptionType = ModuleOptionType.Text },
            })
            .Add(c => c.ValidateKey, (_, _) => true)
            .Add(c => c.CanDelete, _ => true));

    private static async Task Filter(IRenderedComponent<ModuleOptionDeclarationCollectionGrid> component, string text)
    {
        var filterInput = component.Find(".filter input");

        await filterInput.InputAsync(new ChangeEventArgs { Value = text });
        await filterInput.BlurAsync();
    }

    private static List<string> RenderedKeys(IRenderedComponent<ModuleOptionDeclarationCollectionGrid> component)
        => [.. component.FindAll("tbody tr td.key-column").Select(cell => cell.TextContent.Trim())];

    [Fact]
    public async Task Should_not_match_the_hidden_value_of_a_password_option()
    {
        // Arrange
        await using var ctx = SetupTestContext();
        var component = RenderGrid(ctx);

        // Act
        await Filter(component, "ecret");

        // Assert
        RenderedKeys(component).Should().BeEmpty();
    }

    [Fact]
    public async Task Should_match_a_password_option_by_its_key()
    {
        // Arrange
        await using var ctx = SetupTestContext();
        var component = RenderGrid(ctx);

        // Act
        await Filter(component, "PackagePass");

        // Assert
        RenderedKeys(component).Should().Equal("PackagePassword");
    }

    [Fact]
    public async Task Should_match_a_non_password_option_by_its_value()
    {
        // Arrange
        await using var ctx = SetupTestContext();
        var component = RenderGrid(ctx);

        // Act
        await Filter(component, "admi");

        // Assert
        RenderedKeys(component).Should().Equal("PackageUser");
    }
}
