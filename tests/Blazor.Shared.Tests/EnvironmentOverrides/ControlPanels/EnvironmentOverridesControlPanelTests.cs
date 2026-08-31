using AngleSharp.Dom;
using AwesomeAssertions;
using Blazor.Shared.EnvironmentOverrides.ControlPanels.Components;
using Blazor.Shared.EnvironmentOverrides.ControlPanels.Extensions;
using Blazor.Shared.EnvironmentOverrides.ControlPanels.Models;
using Blazor.Shared.EnvironmentOverrides.ControlPanels.Services;
using Blazor.Shared.Services;
using Blazor.Shared.Settings.Extensions;
using Blazor.Tests.Tools;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Sdk.Client.ControlPanels.Services;
using Sdk.Testing.Client;
using ViciOne.Ui.Localization.Resources;
using Xunit;

namespace Blazor.Shared.Tests.EnvironmentOverrides.ControlPanels;

public sealed class EnvironmentOverridesControlPanelTests
{
    private static BunitContext SetupTestContext()
    {
        var ctx = new BunitContext();

        ctx.SetupSuiteServices(setup =>
        {
            setup.Services.AddScoped(_ => Substitute.For<ISuiteControlService>())
                .AddControlPanelInfrastructure()
                .AddEnvironmentOverridesControlPanel();
        });

        ctx.JSInterop.ConfigureQuickGridJSInterop();

        return ctx;
    }

    private static IRenderedComponent<EnvironmentOverridesControlPanel> RenderPanel(BunitContext ctx,
        EnvironmentOverridesControlPanelState state,
        Action? onBeginEdit = null)
    {
        var controlPanelRegistry = ctx.Services.GetRequiredService<IControlPanelRegistry<SharedClientModule>>().First();

        return ctx.Render<EnvironmentOverridesControlPanel>(builder =>
        {
            builder.Add(c => c.State, state)
                .AddCascadingValue(controlPanelRegistry);

            if (onBeginEdit is not null)
                builder.Add(c => c.OnBeginEdit, onBeginEdit);
        });
    }

    private static List<IElement> FindRows(IRenderedComponent<EnvironmentOverridesControlPanel> component)
        => [.. component.FindAll("tbody tr")];

    private static IElement FindButtonByTitle(IRenderedComponent<EnvironmentOverridesControlPanel> component, string title)
        => component.FindAll("button").Single(b => b.GetAttribute("title") == title);

    private static List<IElement> FindRestartButtons(IRenderedComponent<EnvironmentOverridesControlPanel> component)
        => [.. component.FindAll("button")
            .Where(b => b.TextContent.Contains(CommonVocabulary.Restart, StringComparison.Ordinal))];

    private static async Task SelectRow(IElement row)
        => await row.QuerySelector("input[type=checkbox]")!.InputAsync(new ChangeEventArgs { Value = true });

    [Fact]
    public async Task Should_render_one_grid_row_per_entry()
    {
        // Arrange
        var state = new EnvironmentOverridesControlPanelState();
        state.Initialize(new Dictionary<string, string>
        {
            ["MY_KEY"] = "value",
            ["OTHER_KEY"] = "other value",
        });

        await using var ctx = SetupTestContext();

        // Act
        var component = RenderPanel(ctx, state);

        // Assert
        var rows = FindRows(component);
        rows.Should().HaveCount(2);

        rows[0].QuerySelector("td.name-column")!.TextContent.Should().Be("MY_KEY");
        rows[0].QuerySelector("td.value-column")!.TextContent.Should().Be("value");
        rows[1].QuerySelector("td.name-column")!.TextContent.Should().Be("OTHER_KEY");
        rows[1].QuerySelector("td.value-column")!.TextContent.Should().Be("other value");

        FindButtonByTitle(component, CommonVocabulary.Add).Should().NotBeNull();
        FindButtonByTitle(component, CommonVocabulary.Delete).Should().NotBeNull();
    }

    [Fact]
    public async Task Should_render_load_error_instead_of_an_editable_grid()
    {
        // Arrange
        var state = new EnvironmentOverridesControlPanelState { LoadError = "The override file cannot be parsed." };

        await using var ctx = SetupTestContext();

        // Act
        var component = RenderPanel(ctx, state);

        // Assert
        component.Markup.Should().Contain("The override file cannot be parsed.");
        component.FindAll(".quickgrid").Should().BeEmpty();
        component.FindAll("input").Should().BeEmpty();
    }

    [Fact]
    public async Task Should_not_offer_a_restart_while_the_edits_are_unsaved()
    {
        // Arrange
        var state = new EnvironmentOverridesControlPanelState();
        state.Initialize(new Dictionary<string, string> { ["MY_KEY"] = "value" });

        await using var ctx = SetupTestContext();

        var component = RenderPanel(ctx, state);

        // Act
        await FindButtonByTitle(component, CommonVocabulary.EditVerb).ClickAsync();

        var valueInput = FindRows(component)[0].QuerySelector("td.value-column input")!;
        await valueInput.InputAsync(new ChangeEventArgs { Value = "edited" });
        await valueInput.BlurAsync();

        await FindButtonByTitle(component, CommonVocabulary.Save).ClickAsync();

        // Assert
        state.Entries.Should().ContainSingle().Which.Value.Should().Be("edited");
        FindRestartButtons(component).Should().BeEmpty();
    }

    [Fact]
    public async Task Should_offer_a_restart_once_the_overrides_were_saved()
    {
        // Arrange
        var state = new EnvironmentOverridesControlPanelState { RestartRequired = true };
        state.Initialize(new Dictionary<string, string> { ["MY_KEY"] = "value" });

        await using var ctx = SetupTestContext();

        // Act
        var component = RenderPanel(ctx, state);

        // Assert
        FindRestartButtons(component).Should().ContainSingle();
    }

    [Fact]
    public async Task Should_mark_state_dirty_when_adding_a_row()
    {
        // Arrange
        var state = new EnvironmentOverridesControlPanelState();
        state.Initialize(new Dictionary<string, string>());

        await using var ctx = SetupTestContext();

        var beginEditRaised = false;

        var component = RenderPanel(ctx, state, () => beginEditRaised = true);

        // Act
        await FindButtonByTitle(component, CommonVocabulary.Add).ClickAsync();

        // Assert
        state.Entries.Should().ContainSingle();
        beginEditRaised.Should().BeTrue();
    }

    [Fact]
    public async Task Should_drop_an_added_row_that_is_cancelled_instead_of_saved()
    {
        // Arrange
        var state = new EnvironmentOverridesControlPanelState();
        state.Initialize(new Dictionary<string, string>());

        await using var ctx = SetupTestContext();

        var component = RenderPanel(ctx, state);

        // Act
        await FindButtonByTitle(component, CommonVocabulary.Add).ClickAsync();
        await FindButtonByTitle(component, CommonVocabulary.Cancel).ClickAsync();

        // Assert
        state.Entries.Should().BeEmpty();
        FindRows(component).Should().BeEmpty();
    }

    [Fact]
    public async Task Should_drop_an_added_row_when_another_row_is_opened_for_edit()
    {
        // Arrange
        var state = new EnvironmentOverridesControlPanelState();
        state.Initialize(new Dictionary<string, string> { ["MY_KEY"] = "value" });

        await using var ctx = SetupTestContext();

        var component = RenderPanel(ctx, state);

        // Act
        await FindButtonByTitle(component, CommonVocabulary.Add).ClickAsync();
        await FindButtonByTitle(component, CommonVocabulary.EditVerb).ClickAsync();

        // Assert
        state.Entries.Should().ContainSingle().Which.Name.Should().Be("MY_KEY");
        FindRows(component).Should().ContainSingle();
    }

    [Fact]
    public async Task Should_keep_an_existing_row_that_is_cancelled_instead_of_saved()
    {
        // Arrange
        var state = new EnvironmentOverridesControlPanelState();
        state.Initialize(new Dictionary<string, string> { ["MY_KEY"] = "value" });

        await using var ctx = SetupTestContext();

        var component = RenderPanel(ctx, state);

        // Act
        await FindButtonByTitle(component, CommonVocabulary.EditVerb).ClickAsync();

        var valueInput = FindRows(component)[0].QuerySelector("td.value-column input")!;
        await valueInput.InputAsync(new ChangeEventArgs { Value = "edited" });
        await valueInput.BlurAsync();
        await FindButtonByTitle(component, CommonVocabulary.Cancel).ClickAsync();

        // Assert
        state.Entries.Should().ContainSingle().Which.Value.Should().Be("value");
    }

    [Fact]
    public async Task Should_remove_the_selected_row_when_two_rows_look_alike()
    {
        // Arrange — seeded past Initialize on purpose: the stored overrides cannot hold a name
        // twice, but the grid has to until the duplicate is either edited or refused on save.
        var state = new EnvironmentOverridesControlPanelState();
        state.Entries =
        [
            new EnvironmentOverrideEntry { Name = "MY_KEY", Value = "value" },
            new EnvironmentOverrideEntry { Name = "MY_KEY", Value = "value" },
        ];

        var firstEntry = state.Entries[0];

        await using var ctx = SetupTestContext();

        var component = RenderPanel(ctx, state);

        // Act
        await SelectRow(FindRows(component)[1]);
        await FindButtonByTitle(component, CommonVocabulary.Delete).ClickAsync();

        // Assert
        state.Entries.Should().ContainSingle().Which.Should().BeSameAs(firstEntry);
    }

    [Fact]
    public async Task Should_order_the_rows_case_insensitively()
    {
        // Arrange
        var state = new EnvironmentOverridesControlPanelState();
        state.Initialize(new Dictionary<string, string>
        {
            ["CC"] = "third",
            ["AA"] = "first",
            ["bb"] = "second",
        });

        await using var ctx = SetupTestContext();

        // Act
        var component = RenderPanel(ctx, state);

        // Assert
        var rows = FindRows(component);
        rows.Select(r => r.QuerySelector("td.name-column")!.TextContent)
            .Should().Equal("AA", "bb", "CC");
    }

    [Fact]
    public async Task Should_order_two_names_that_differ_only_in_case_deterministically()
    {
        // Arrange
        var state = new EnvironmentOverridesControlPanelState();
        state.Initialize(new Dictionary<string, string>
        {
            ["Path"] = "mixed case",
            ["PATH"] = "upper case",
        });

        await using var ctx = SetupTestContext();

        // Act
        var component = RenderPanel(ctx, state);

        // Assert
        var rows = FindRows(component);
        rows.Select(r => r.QuerySelector("td.name-column")!.TextContent)
            .Should().Equal("PATH", "Path");
    }

    [Fact]
    public async Task Should_only_render_the_rows_matching_the_filter()
    {
        // Arrange
        var state = new EnvironmentOverridesControlPanelState();
        state.Initialize(new Dictionary<string, string>
        {
            ["MY_KEY"] = "value",
            ["OTHER_KEY"] = "other value",
        });

        await using var ctx = SetupTestContext();

        var component = RenderPanel(ctx, state);

        // Act
        var filterInput = component.Find(".filter input");
        await filterInput.InputAsync(new ChangeEventArgs { Value = "other" });
        await filterInput.BlurAsync();

        // Assert
        var rows = FindRows(component);
        rows.Should().ContainSingle();
        rows[0].QuerySelector("td.name-column")!.TextContent.Should().Be("OTHER_KEY");
    }

    [Fact]
    public async Task Should_keep_a_row_with_an_invalid_name_in_edit_mode()
    {
        // Arrange
        var state = new EnvironmentOverridesControlPanelState();
        state.Initialize(new Dictionary<string, string> { ["MY_KEY"] = "value" });

        await using var ctx = SetupTestContext();

        var component = RenderPanel(ctx, state);

        // Act
        await FindButtonByTitle(component, CommonVocabulary.EditVerb).ClickAsync();

        var nameInput = FindRows(component)[0].QuerySelector("td.name-column input")!;
        await nameInput.InputAsync(new ChangeEventArgs { Value = "HAS SPACE" });
        await nameInput.BlurAsync();

        await FindButtonByTitle(component, CommonVocabulary.Save).ClickAsync();

        // Assert
        
        // check state did not update with invalid name
        state.Entries.Should().ContainSingle().Which.Name.Should().Be("MY_KEY");
        // check input is still there -> we are still in edit mode and did not commit the update
        FindRows(component)[0].QuerySelector("td.name-column input").Should().NotBeNull();
    }
}
