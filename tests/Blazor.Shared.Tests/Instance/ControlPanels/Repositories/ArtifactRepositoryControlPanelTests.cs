using System.Globalization;
using Blazor.Shared.Instance.ControlPanels.Repositories;
using Blazor.Shared.Instance.ControlPanels.Repositories.Models;
using Blazor.Shared.Instance.ControlPanels.Repositories.Services;
using Blazor.Shared.Settings.Extensions;
using Bunit;
using Core.Shared.Instance.Contracts;
using Microsoft.Extensions.DependencyInjection;
using Sdk.Client.Components.Settings;
using Sdk.Client.ControlPanels.Extensions;
using Sdk.Testing.Client;
using ViciOne.Ui.Blazor.Components.TestingHelpers.TextBox.Extensions;
using ViciOne.Ui.Localization.Resources;
using ArtifactRepositoryControlPanelStrings = Blazor.Shared.Instance.ControlPanels.Repositories.Localization.ArtifactRepositoryControlPanel;

namespace Blazor.Shared.Tests.Instance.ControlPanels.Repositories;

public class ArtifactRepositoryControlPanelTests
{
    private readonly IArtifactRepositoryClientService _repositoryClientService = Substitute.For<IArtifactRepositoryClientService>();

    private BunitContext SetupContext()
    {
        var ctx = new BunitContext();
        ctx.SetupSuiteServices();
        ctx.JSInterop.SetupForTextBox();
        ctx.SetupControlPanelServices();
        ctx.Services.AddControlPanelInfrastructure();
        ctx.Services.AddSingleton(_repositoryClientService);

        ctx.Services.AddControlPanel<SharedClientModule, ArtifactRepositoryControlPanel, ArtifactRepositoryControlPanelState>()
               .WithAutoDiscovery<ArtifactRepositoryControlPanelDescriptor>()
               .WithSaveHandler<ArtifactRepositoryControlPanelSaveHandler>()
               .WithResetHandler<ArtifactRepositoryControlPanelResetHandler>();

        return ctx;
    }

    [Fact]
    public void Should_render_component()
    {
        // Arrange
        var source = new ArtifactRepository { Id = Guid.NewGuid(), Endpoint = "https://update.ifm.com" };
        var model = new ArtifactRepositoryModel(source);
        var state = new ArtifactRepositoryControlPanelState() { RepositoryId = model.Id, Repository = model };

        using var ctx = SetupContext();

        // Act
        var component = ctx.Render<ArtifactRepositoryControlPanel>(p => p.Add(c => c.State, state));

        // Assert
        component.Should().NotBeNull();
    }

    [Fact]
    public void Should_show_the_add_title_for_a_new_repository()
    {
        // Arrange
        var model = new ArtifactRepositoryModel(new ArtifactRepository { Id = Guid.NewGuid(), Endpoint = "https://update.ifm.com" });
        var state = new ArtifactRepositoryControlPanelState { Repository = model };

        using var ctx = SetupContext();

        // Act
        var banner = ctx.Render<ArtifactRepositoryControlPanel>(p => p.Add(c => c.State, state)).FindComponent<DescriptionBanner>().Instance;

        // Assert
        banner.Title.Should().Be(ArtifactRepositoryControlPanelStrings.DescriptionBannerTitleOnAdd);
    }

    [Fact]
    public void Should_show_the_edit_title_for_an_existing_repository()
    {
        // Arrange
        var model = new ArtifactRepositoryModel(new ArtifactRepository { Id = Guid.NewGuid(), Endpoint = "https://update.ifm.com", Name = "ifm" });
        var state = new ArtifactRepositoryControlPanelState { RepositoryId = model.Id, Repository = model };

        using var ctx = SetupContext();

        // Act
        var banner = ctx.Render<ArtifactRepositoryControlPanel>(p => p.Add(c => c.State, state)).FindComponent<DescriptionBanner>().Instance;

        // Assert
        banner.Title.Should().Be(string.Format(CultureInfo.CurrentCulture, UserActions.EditSomething, "ifm"));
    }

    [Fact]
    public void Should_title_an_unnamed_repository_by_its_endpoint()
    {
        // Arrange
        var model = new ArtifactRepositoryModel(new ArtifactRepository { Id = Guid.NewGuid(), Endpoint = "https://update.ifm.com" });
        var state = new ArtifactRepositoryControlPanelState { RepositoryId = model.Id, Repository = model };

        using var ctx = SetupContext();

        // Act
        var banner = ctx.Render<ArtifactRepositoryControlPanel>(p => p.Add(c => c.State, state)).FindComponent<DescriptionBanner>().Instance;

        // Assert
        banner.Title.Should().Be(string.Format(CultureInfo.CurrentCulture, UserActions.EditSomething, "https://update.ifm.com"));
    }
}
