using AngleSharp.Dom;
using Blazor.Shared.Connections.ControlPanels.Tags;
using Blazor.Shared.Connections.ControlPanels.Tags.Services;
using Blazor.Shared.Connections.Extensions;
using Blazor.Shared.Connections.Services;
using Blazor.Shared.Settings.Extensions;
using Blazor.Tests.Tools;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Sdk.Client.ControlPanels.Services;
using Sdk.Connections.Contracts;
using Sdk.Connections.Events;
using Sdk.Messaging;
using Sdk.Testing.Client;
using ViciOne.Ui.Blazor.Components.Grid.Services;
using ViciOne.Ui.MonochromeIcons.Core.Enums;

namespace Blazor.Shared.Tests.Connections.ControlPanels;

public class ConnectionsTagsControlPanelTests
{
    private readonly ISuiteConnectionService _connectionService = Substitute.For<ISuiteConnectionService>();

    private BunitContext SetupTestContext(Action<ClientServiceConfigurator>? configure = null,
        Action<IControlPanelRequest>? controlPanelRequestSetup = null)
    {
        var ctx = new BunitContext();
        ctx.Services.AddSingleton(_connectionService);
        ctx.Services.AddControlPanelInfrastructure();
        ctx.Services.AddConnectionsControlPanel();

        ctx.SetupBlazorUiComponents(setup =>
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

    private static IReadOnlyList<IElement> FindItemSelectCheckboxesInGrid(IRenderedComponent<ConnectionsTagsControlPanel> component)
        => component.FindAll(".item-select-column .check-box");

    private static void SelectCheckbox(IRenderedComponent<ConnectionsTagsControlPanel> component, int selectedRow, bool checkboxState = true)
    {
        var checkboxes = FindItemSelectCheckboxesInGrid(component);
        var chkBoxSelect = checkboxes[selectedRow];
        var input = chkBoxSelect.QuerySelector("input");
        input?.Input(new ChangeEventArgs { Value = checkboxState.ToString() });
    }

    private static IReadOnlyList<IElement> FindValuesGrid(IRenderedComponent<ConnectionsTagsControlPanel> component)
        => component.FindAll(".quickgrid tr");

    private static IElement FindDeleteButton(IRenderedComponent<ConnectionsTagsControlPanel> component)
        => component.FindAll("button").First(b => b.InnerHtml.Contains("monochrome-icon-delete", StringComparison.Ordinal));

    private static IElement FindEditButton(IRenderedComponent<ConnectionsTagsControlPanel> component)
        => component.FindAll("button").First(b => b.InnerHtml.Contains("monochrome-icon-edit", StringComparison.Ordinal));

    public sealed class OnInitializedAsync : ConnectionsTagsControlPanelTests
    {
        [Fact]
        public void Should_render_component()
        {
            // Arrange
            var state = new ConnectionsTagsControlPanelState();
            using var ctx = SetupTestContext();

            // Act + Assert
            Assert.NotNull(ctx.Render<ConnectionsTagsControlPanel>(b =>
                b.Add(c => c.State, state)));
        }
    }

    public sealed class StateChanged : ConnectionsTagsControlPanelTests
    {
        [Fact]
        public void Should_deselect_a_tag_that_is_no_longer_listed()
        {
            // Arrange
            var tags = CreateTags();
            var deletedTag = tags[1];
            var keptTag = tags[2];
            var state = new ConnectionsTagsControlPanelState { Tags = tags.ToDictionary(k => k.Id) };
            using var ctx = SetupTestContext();
            var selection = ctx.Services.GetRequiredKeyedService<IGridItemSelection<Guid>>(typeof(ConnectionsTagsControlPanelServiceKey));

            ctx.Render<ConnectionsTagsControlPanel>(b => b.Add(c => c.State, state));

            selection.Add(deletedTag.Id);
            selection.Add(keptTag.Id);

            // Act
            state.Tags = tags.Where(t => t.Id != deletedTag.Id).ToDictionary(k => k.Id);

            // Assert
            selection.Should().BeEquivalentTo([keptTag.Id]);
        }
    }

    public sealed class OnTagsChanged : ConnectionsTagsControlPanelTests
    {
        [Fact]
        public void Should_remove_a_tag_deleted_elsewhere_from_the_state()
        {
            // Arrange
            var tags = CreateTags();
            var state = new ConnectionsTagsControlPanelState { Tags = tags.ToDictionary(k => k.Id) };
            using var ctx = SetupTestContext();
            ctx.Render<ConnectionsTagsControlPanel>(b => b.Add(c => c.State, state));

            // Act
            _connectionService.TagsChanged += Raise.Event<Func<TagsChanged, Task>>(
                new TagsChanged(CrudAction.Deleted, [tags[1]]) { CorrelationId = Guid.NewGuid() });

            // Assert
            state.Tags.Keys.Should().BeEquivalentTo([tags[0].Id, tags[2].Id]);
        }
    }

    public sealed class OnCreateTag : ConnectionsTagsControlPanelTests
    {
        [Fact(Skip = "Incompatibility with bunit and virtual scrolling.")]
        public async Task Should_send_control_request()
        {
            // Arrange
            IControlPanelRequest? controlPanelRequest = null;

            ConnectionsTagsControlPanelState? state = null;
            var tags = CreateTags();

            await using var ctx = SetupTestContext(setup =>
            {
                state = new()
                {
                    Tags = tags.ToDictionary(k => k.Id)
                };
            },
            controlPanelRequestSetup =>
            {
                controlPanelRequest = controlPanelRequestSetup;
            });

            // Act + Assert
            var component = ctx.Render<ConnectionsTagsControlPanel>(b =>
                b.Add(c => c.State, state));

            var addButton = component.FindGridActionButton(MonochromeIconName.PlusSlim);
            var disabledAddButton = addButton.HasAttribute("disabled");

            Assert.False(disabledAddButton);
            await addButton.ClickAsync();

            Assert.NotNull(controlPanelRequest);
            await controlPanelRequest.Received(1)
                .Send<TagControlPanel, TagControlPanelState>(Arg.Any<Action<TagControlPanelState>>());
        }
    }

    public sealed class OnEditTag : ConnectionsTagsControlPanelTests
    {
        [Fact(Skip = "Incompatibility with bunit and virtual scrolling.")]
        public async Task Should_enable_button_when_one_row_is_selected()
        {
            // Arrange
            IControlPanelRequest? controlPanelRequest = null;

            ConnectionsTagsControlPanelState? state = null;
            var tags = CreateTags();

            await using var ctx = SetupTestContext(setup =>
            {
                state = new()
                {
                    Tags = tags.ToDictionary(k => k.Id)
                };
            },
             controlPanelRequestSetup =>
            {
                controlPanelRequest = controlPanelRequestSetup;
            });

            // Act + Assert
            var component = ctx.Render<ConnectionsTagsControlPanel>(b =>
                b.Add(c => c.State, state));

            // Selects the first grid row by clicking its checkbox.
            SelectCheckbox(component, 1);

            var editButton = FindEditButton(component);
            var disabledAddButton = editButton.HasAttribute("disabled");

            Assert.False(disabledAddButton);
            await editButton.ClickAsync();

            Assert.NotNull(controlPanelRequest);
            await controlPanelRequest.Received(1).Send<TagControlPanel, TagControlPanelState>(
                Arg.Any<Action<TagControlPanelState>>());
        }

        [Fact(Skip = "Incompatibility with bunit and virtual scrolling.")]
        public void Should_disable_button_when_two_rows_are_selected()
        {
            // Arrange
            IControlPanelRequest? controlPanelRequest = null;

            ConnectionsTagsControlPanelState? state = null;
            var tags = CreateTags();

            using var ctx = SetupTestContext(setup =>
            {
                state = new()
                {
                    Tags = tags.ToDictionary(k => k.Id)
                };
            },
             controlPanelRequestSetup =>
            {
                controlPanelRequest = controlPanelRequestSetup;
            });

            // Act
            var component = ctx.Render<ConnectionsTagsControlPanel>(b =>
                b.Add(c => c.State, state));

            // Selects the first and second grid rows by clicking their checkboxes.
            SelectCheckbox(component, 1);
            SelectCheckbox(component, 2);

            var editButton = FindEditButton(component);
            var disabledEditButton = editButton.HasAttribute("disabled");

            // Assert
            Assert.True(disabledEditButton);
        }

        [Fact(Skip = "Incompatibility with bunit and virtual scrolling.")]
        public async Task Should_send_command_on_mediator_when_arrow_button_clicked()
        {
            // Arrange
            IControlPanelRequest? controlPanelRequest = null;

            ConnectionsTagsControlPanelState? state = null;
            var tags = CreateTags();

            await using var ctx = SetupTestContext(setup =>
            {
                state = new()
                {
                    Tags = tags.ToDictionary(k => k.Id)
                };
            },
             controlPanelRequestSetup =>
            {
                controlPanelRequest = controlPanelRequestSetup;
            });

            // Act + Assert
            var component = ctx.Render<ConnectionsTagsControlPanel>(b =>
                b.Add(c => c.State, state));

            var gridValues = FindValuesGrid(component);

            // Edits the first grid row.
            var secondRow = gridValues[1];
            var arrowButton = secondRow.QuerySelector(".navigate-button");

            Assert.NotNull(arrowButton);
            await arrowButton.ClickAsync();

            await controlPanelRequest!.Received(1).Send<TagControlPanel, TagControlPanelState>(
                Arg.Any<Action<TagControlPanelState>>());
        }
    }

    public sealed class DeleteSelectedTags : ConnectionsTagsControlPanelTests
    {
        [Fact(Skip = "Incompatibility with bunit and virtual scrolling.")]
        public void Should_add_correct_tag_to_list()
        {
            // Arrange
            IControlPanelRequest? controlPanelRequest = null;

            var tags = CreateTags();

            ConnectionsTagsControlPanelState? state = null;

            using var ctx = SetupTestContext(setup =>
            {
                state = new()
                {
                    Tags = tags.ToDictionary(k => k.Id)
                };
            },
            controlPanelRequestSetup =>
            {
                controlPanelRequest = controlPanelRequestSetup;
            });

            var component = ctx.Render<ConnectionsTagsControlPanel>(b =>
                b.Add(c => c.State, state));

            // Selects an unprotected tag, the second grid row.
            SelectCheckbox(component, 2);

            // Act + Assert
            var deleteButton = FindDeleteButton(component);

            // The delete button is enabled.
            var disabledDeleteButton = deleteButton.HasAttribute("disabled");
            Assert.False(disabledDeleteButton);

            deleteButton.Click();

            // Added Tag to deleting list
            state?.DeletingTags.Count.Should().Be(1);
            state?.DeletingTags.First().Id.Should().Be(tags.First(t => !t.Protected).Id);
        }

        [Fact(Skip = "Incompatibility with bunit and virtual scrolling.")]
        public void Should_disable_delete_button_if_tag_is_protected()
        {
            // Arrange
            IControlPanelRequest? controlPanelRequest = null;

            ConnectionsTagsControlPanelState? state = null;
            var tags = CreateTags();

            using var ctx = SetupTestContext(setup =>
            {
                state = new()
                {
                    Tags = tags.ToDictionary(k => k.Id)
                };
            },
             controlPanelRequestSetup =>
            {
                controlPanelRequest = controlPanelRequestSetup;
            });

            var component = ctx.Render<ConnectionsTagsControlPanel>(b =>
                b.Add(c => c.State, state));

            // Selects a protected tag, the first grid row.
            SelectCheckbox(component, 1);

            // Act + Assert
            var deleteButton = FindDeleteButton(component);

            // The delete button is disabled.
            var disabledDeleteButton = deleteButton.HasAttribute("disabled");
            Assert.True(disabledDeleteButton);
        }
    }
}
