using AngleSharp.Dom;
using AwesomeAssertions;
using Blazor.Shared.Settings.Extensions;
using Blazor.Shared.Connections.ControlPanels;
using Blazor.Shared.Connections.Extensions;
using Blazor.Shared.Connections.Services;
using Blazor.Tests.Tools;
using Bunit;
using Microsoft.AspNetCore.Components;
using NSubstitute;
using Sdk.Client.ControlPanels.Services;
using Sdk.Client.Infrastructure;
using Sdk.Connections.Contracts;
using Sdk.Testing.Client;
using ViciOne.Ui.MonochromeIcons.Core.Enums;
using Xunit;

namespace Blazor.Shared.Tests.Connections.ControlPanels;

public class TagsControlPanelPageContentTests
{
    private static BunitContext SetupTestContext(Action<ClientServiceConfigurator>? configure = null,
        Action<IControlPanelRequest>? controlPanelRequestSetup = null)
    {
        var ctx = new BunitContext();
        ctx.Services.AddControlPanelInfrastructure();
        ctx.Services.AddConnectionsControlPanel();

        ctx.SetupSuiteServicesWithBlazorDx(setup =>
        {
            configure?.Invoke(setup);
        });

        ctx.SetupControlPanelServices(setup =>
        {
            controlPanelRequestSetup?.Invoke(setup);
        });

        ctx.JSInterop.SetupModule();

        return ctx;
    }

    private static List<Tag> CreateTags()
        =>
        [
            new()
            {
                Id = Guid.NewGuid(),
                Text = "Test Tag1 XYZ",
                Protected = true,
            },
            new()
            {
                Id = Guid.NewGuid(),
                Text = "Test Tag2 ABC",
                Protected = false,
            },
            new()
            {
                Id = Guid.NewGuid(),
                Text = "Test Tag3 987",
                Protected = false,
            },
        ];

    private static IReadOnlyList<IElement> FindItemSelectCheckboxesInGrid(IRenderedComponent<TagsControlPanelPageContent> component)
        => component.FindAll(".item-select-column .check-box");

    private static void SelectCheckbox(IRenderedComponent<TagsControlPanelPageContent> component, int selectedRow, bool checkboxState = true)
    {
        var checkboxes = FindItemSelectCheckboxesInGrid(component);
        var chkBoxSelect = checkboxes[selectedRow];
        var input = chkBoxSelect.QuerySelector("input");
        input?.Input(new ChangeEventArgs { Value = checkboxState.ToString() });
    }

    private static IReadOnlyList<IElement> FindValuesGrid(IRenderedComponent<TagsControlPanelPageContent> component)
        => component.FindAll(".quickgrid tr");

    private static IElement FindDeleteButton(IRenderedComponent<TagsControlPanelPageContent> component)
        => component.FindAll("button").First(b => b.InnerHtml.Contains("monochrome-icon-delete", StringComparison.Ordinal));

    private static IElement FindEditButton(IRenderedComponent<TagsControlPanelPageContent> component)
        => component.FindAll("button").First(b => b.InnerHtml.Contains("monochrome-icon-edit", StringComparison.Ordinal));

    public class OnInitializedAsync : TagsControlPanelPageContentTests
    {
        [Fact]
        public void Should_render_component()
        {
            // Arrange
            using var state = new ConnectionsControlPanelState(Substitute.For<IUiMediator>());
            using var ctx = SetupTestContext();

            // Act + Assert
            Assert.NotNull(ctx.Render<TagsControlPanelPageContent>(b =>
                b.Add(p => p.State, state)
                .Add(p => p.BeginEdit, () => Task.CompletedTask)));
        }
    }

    public class OnCreateTag : TagsControlPanelPageContentTests
    {
        [Fact(Skip = "Incompatibility with bunit and virtual scrolling.")]
        public async Task Should_send_control_request()
        {
            // Arrange
            IControlPanelRequest? controlPanelRequest = null;

            ConnectionsControlPanelState? state = null;
            var tags = CreateTags();

            await using var ctx = SetupTestContext(setup =>
            {
                state = new(setup.ClientMediator)
                {
                    Tags = tags.ToDictionary(k => k.Id)
                };
            },
            controlPanelRequestSetup =>
            {
                controlPanelRequest = controlPanelRequestSetup;
            });

            // Act + Assert
            var component = ctx.Render<TagsControlPanelPageContent>(b =>
                b.Add(p => p.State, state)
                .Add(p => p.BeginEdit, () => Task.CompletedTask));

            var addButton = component.FindGridActionButton(MonochromeIconName.PlusSlim);
            var disabledAddButton = addButton.HasAttribute("disabled");

            Assert.False(disabledAddButton);
            addButton.Click();

            Assert.NotNull(controlPanelRequest);
            await controlPanelRequest.Received(1)
                .Send<TagControlPanel, TagControlPanelState>(Arg.Any<Action<TagControlPanelState>>());
        }
    }

    public class OnEditTag : TagsControlPanelPageContentTests
    {
        [Fact(Skip = "Incompatibility with bunit and virtual scrolling.")]
        public async Task One_row_is_selected_than_button_should_enable()
        {
            // Arrange
            IControlPanelRequest? controlPanelRequest = null;

            ConnectionsControlPanelState? state = null;
            var tags = CreateTags();

            await using var ctx = SetupTestContext(setup =>
            {
                state = new(setup.ClientMediator)
                {
                    Tags = tags.ToDictionary(k => k.Id)
                };
            },
             controlPanelRequestSetup =>
            {
                controlPanelRequest = controlPanelRequestSetup;
            });

            // Act + Assert
            var component = ctx.Render<TagsControlPanelPageContent>(b =>
                b.Add(p => p.State, state)
                .Add(p => p.BeginEdit, () => Task.CompletedTask));

            // select first row in the grid  - click on checkbox
            SelectCheckbox(component, 1);

            var editButton = FindEditButton(component);
            var disabledAddButton = editButton.HasAttribute("disabled");

            Assert.False(disabledAddButton);
            editButton.Click();

            Assert.NotNull(controlPanelRequest);
            await controlPanelRequest.Received(1).Send<TagControlPanel, TagControlPanelState>(
                Arg.Any<Action<TagControlPanelState>>());
        }

        [Fact(Skip = "Incompatibility with bunit and virtual scrolling.")]
        public void Two_rows_are_selected_than_button_should_disable()
        {
            // Arrange
            IControlPanelRequest? controlPanelRequest = null;

            ConnectionsControlPanelState? state = null;
            var tags = CreateTags();

            using var ctx = SetupTestContext(setup =>
            {
                state = new(setup.ClientMediator)
                {
                    Tags = tags.ToDictionary(k => k.Id)
                };
            },
             controlPanelRequestSetup =>
            {
                controlPanelRequest = controlPanelRequestSetup;
            });

            // Act
            var component = ctx.Render<TagsControlPanelPageContent>(b =>
                b.Add(p => p.State, state)
                .Add(p => p.BeginEdit, () => Task.CompletedTask));

            // first and second row in grid select - click on checkbox
            SelectCheckbox(component, 1);
            SelectCheckbox(component, 2);

            var editButton = FindEditButton(component);
            var disabledEditButton = editButton.HasAttribute("disabled");

            // Assert
            Assert.True(disabledEditButton);
        }

        [Fact(Skip = "Incompatibility with bunit and virtual scrolling.")]
        public async Task Arrow_button_should_send_command_on_mediator()
        {
            // Arrange
            IControlPanelRequest? controlPanelRequest = null;

            ConnectionsControlPanelState? state = null;
            var tags = CreateTags();

            await using var ctx = SetupTestContext(setup =>
            {
                state = new(setup.ClientMediator)
                {
                    Tags = tags.ToDictionary(k => k.Id)
                };
            },
             controlPanelRequestSetup =>
            {
                controlPanelRequest = controlPanelRequestSetup;
            });

            // Act + Assert
            var component = ctx.Render<TagsControlPanelPageContent>(b =>
                b.Add(p => p.State, state)
                .Add(p => p.BeginEdit, () => Task.CompletedTask));

            var gridValues = FindValuesGrid(component);

            // first row in grid edit
            var secondRow = gridValues[1];
            var arrowButton = secondRow.QuerySelector(".navigate-button");

            Assert.NotNull(arrowButton);
            arrowButton.Click();

            await controlPanelRequest!.Received(1).Send<TagControlPanel, TagControlPanelState>(
                Arg.Any<Action<TagControlPanelState>>());
        }
    }

    public class DeleteSelectedTags : TagsControlPanelPageContentTests
    {
        [Fact(Skip = "Incompatibility with bunit and virtual scrolling.")]
        public void Adds_correct_tag_to_list()
        {
            // Arrange
            IControlPanelRequest? controlPanelRequest = null;

            var tags = CreateTags();

            ConnectionsControlPanelState? state = null;

            using var ctx = SetupTestContext(setup =>
            {
                state = new(setup.ClientMediator)
                {
                    Tags = tags.ToDictionary(k => k.Id)
                };
            },
            controlPanelRequestSetup =>
            {
                controlPanelRequest = controlPanelRequestSetup;
            });

            var component = ctx.Render<TagsControlPanelPageContent>(b =>
                b.Add(p => p.State, state)
                .Add(p => p.BeginEdit, () => Task.CompletedTask));

            // select a tag where protected = false (second row in the grid)  - click on checkbox
            SelectCheckbox(component, 2);

            // Act + Assert
            var deleteButton = FindDeleteButton(component);

            // delete-Button is enabled
            var disabledDeleteButton = deleteButton.HasAttribute("disabled");
            Assert.False(disabledDeleteButton);

            deleteButton.Click();

            // Added Tag to deleting list
            state?.DeletingTags.Count.Should().Be(1);
            state?.DeletingTags.First().Id.Should().Be(tags.First(t => !t.Protected).Id);
        }

        [Fact(Skip = "Incompatibility with bunit and virtual scrolling.")]
        public void Disables_delete_button_if_tag_is_protected()
        {
            // Arrange
            IControlPanelRequest? controlPanelRequest = null;

            ConnectionsControlPanelState? state = null;
            var tags = CreateTags();

            using var ctx = SetupTestContext(setup =>
            {
                state = new(setup.ClientMediator)
                {
                    Tags = tags.ToDictionary(k => k.Id)
                };
            },
             controlPanelRequestSetup =>
            {
                controlPanelRequest = controlPanelRequestSetup;
            });

            var component = ctx.Render<TagsControlPanelPageContent>(b =>
                b.Add(p => p.State, state)
                .Add(p => p.BeginEdit, () => Task.CompletedTask));

            // select a tag where protected = true (first row in the grid)  - click on checkbox
            SelectCheckbox(component, 1);

            // Act + Assert
            var deleteButton = FindDeleteButton(component);

            // delete-Button is disabled
            var disabledDeleteButton = deleteButton.HasAttribute("disabled");
            Assert.True(disabledDeleteButton);
        }
    }
}
