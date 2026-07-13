using AwesomeAssertions;
using Blazor.Shared.Settings.Extensions;
using Blazor.Shared.Connections.ControlPanels;
using Blazor.Shared.Connections.ControlPanels.Tags;
using Blazor.Shared.Connections.ControlPanels.Tags.Services;
using Blazor.Shared.Connections.Extensions;
using Blazor.Shared.Connections.Services;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Sdk.Client.ControlPanels.Services;
using Sdk.Connections.Contracts;
using Sdk.Testing.Client;
using ViciOne.Ui.Blazor.Components.CheckBox;
using ViciOne.Ui.Localization.Resources;
using Xunit;

namespace Blazor.Shared.Tests.Connections.ControlPanels;

public sealed class TagControlPanelTests
{
    private readonly ISuiteConnectionService _connectionService = Substitute.For<ISuiteConnectionService>();

    private static BunitContext SetupTestContext(ISuiteConnectionService connectionService)
    {
        var ctx = new BunitContext();

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
}
