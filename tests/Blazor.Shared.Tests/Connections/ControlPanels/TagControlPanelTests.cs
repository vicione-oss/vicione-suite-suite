using System.Globalization;
using Blazor.Shared.Connections.Contracts;
using Blazor.Shared.Settings.Extensions;
using Blazor.Shared.Connections.ControlPanels.Tags;
using Blazor.Shared.Connections.ControlPanels.Tags.Services;
using Blazor.Shared.Connections.Extensions;
using Blazor.Shared.Connections.Services;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Sdk.Client.ControlPanels.Services;
using Sdk.Connections.Contracts;
using Sdk.Testing.Client;
using ViciOne.Ui.Blazor.Components.CheckBox;
using ViciOne.Ui.Blazor.Components.TestingHelpers.TextBox.Extensions;
using ViciOne.Ui.Localization.Resources;
using Sdk.Client.Components.Settings;
using TagControlPanelStrings = Blazor.Shared.Connections.ControlPanels.Tags.Localization.TagControlPanel;

namespace Blazor.Shared.Tests.Connections.ControlPanels;

public sealed class TagControlPanelTests
{
    private readonly ISuiteConnectionService _connectionService = Substitute.For<ISuiteConnectionService>();

    private static BunitContext SetupTestContext(ISuiteConnectionService connectionService)
    {
        var ctx = new BunitContext();

        ctx.JSInterop.SetupForTextBox();

        ctx.SetupSuiteServices(setup =>
        {
            setup.Services.AddScoped(_ => connectionService)
                .AddSingleton(Substitute.For<IActiveControlPanelDescriptorProvider>())
                .AddControlPanelInfrastructure()
                .AddTagControlPanel();
        });

        return ctx;
    }

    [Fact]
    public void Should_render_component()
    {
        // Arrange
        using var ctx = SetupTestContext(_connectionService);

        var state = new TagControlPanelState();

        // Act
        var component = ctx.Render<TagControlPanel>(p => p.Add(c => c.State, state));

        // Assert
        component.Should().NotBeNull();
    }

    [Fact]
    public async Task Should_bind_tag_from_state_id()
    {
        // Arrange
        var tag = new Tag { Id = Guid.NewGuid(), Text = "Hello World" };

        _connectionService.GetTag(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(tag);

        await using var ctx = SetupTestContext(_connectionService);

        var state = new TagControlPanelState { TagId = tag.Id };

        var resetHandler = ctx.Services.GetRequiredService<IControlPanelResetHandler<TagControlPanelState>>();
        await resetHandler.Reset(state, Xunit.TestContext.Current.CancellationToken);

        // Act
        var component = ctx.Render<TagControlPanel>(parameters =>
        {
            parameters.Add(c => c.State, state);
        });

        component.GetSettingsFieldChild<CheckBox<bool>>(CommonVocabulary.Protected);

        // Assert
        component.AssertSettingsFieldTextBox(TechnicalTerms.Id, tag.Id.ToString());
        component.AssertSettingsFieldTextBox(CommonVocabulary.Text, tag.Text);
        component.AssertSettingsFieldCheckBox(CommonVocabulary.Protected, tag.Protected);
    }

    [Fact]
    public async Task Should_show_the_add_title_for_a_new_tag()
    {
        // Arrange
        await using var ctx = SetupTestContext(_connectionService);

        var state = new TagControlPanelState();

        // Act
        var banner = ctx.Render<TagControlPanel>(p => p.Add(c => c.State, state)).FindComponent<DescriptionBanner>().Instance;

        // Assert
        banner.Title.Should().Be(TagControlPanelStrings.DescriptionBannerTitleOnAdd);
    }

    [Fact]
    public async Task Should_show_the_edit_title_for_an_existing_tag()
    {
        // Arrange
        await using var ctx = SetupTestContext(_connectionService);

        var state = new TagControlPanelState { TagId = Guid.NewGuid(), Tag = new EditTagModel { Text = "Line 1" } };

        // Act
        var banner = ctx.Render<TagControlPanel>(p => p.Add(c => c.State, state)).FindComponent<DescriptionBanner>().Instance;

        // Assert
        banner.Title.Should().Be(string.Format(CultureInfo.CurrentCulture, UserActions.EditSomething, "Line 1"));
    }
}
