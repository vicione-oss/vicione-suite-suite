using AwesomeAssertions;
using Blazor.Shared.Settings.Extensions;
using Blazor.Shared.UserManagement.ControlPanels.Roles.Components;
using Blazor.Shared.UserManagement.ControlPanels.Roles.Extensions;
using Blazor.Shared.UserManagement.ControlPanels.Roles.Services;
using Blazor.Tests.Tools;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Sdk.Client.ControlPanels.Services;
using Sdk.Client.Infrastructure;
using Sdk.Messaging;
using Sdk.Testing.Client;
using Sdk.UserManagement.Events;
using ViciOne.Ui.Blazor.Components.TestingHelpers.TextBox.Extensions;
using Xunit;

namespace Blazor.Shared.Tests.UserManagement.ControlPanels.Roles.Components;

public sealed class RolesControlPanelTests
{
    private static BunitContext SetupTestContext()
    {
        var ctx = new BunitContext();

        ctx.JSInterop.SetupForTextBox();
        ctx.JSInterop.ConfigureQuickGridJSInterop();

        ctx.SetupSuiteServices(setup =>
        {
            setup.Services.AddControlPanelInfrastructure()
                .AddRolesControlPanel();
        });

        return ctx;
    }

    private static IRenderedComponent<RolesControlPanel> RenderPanel(BunitContext ctx, RolesControlPanelState state)
    {
        var controlPanelRegistry = ctx.Services.GetRequiredService<IControlPanelRegistry<SharedClientModule>>().First();

        return ctx.Render<RolesControlPanel>(builder => builder.Add(c => c.State, state).AddCascadingValue(controlPanelRegistry));
    }

    private static RolesControlPanelState CreateState(params string[] roleNames)
        => new() { Roles = [.. roleNames.Select(n => new Sdk.UserManagement.Contracts.Role { Name = n })] };

    private static Task Deliver<TEvent>(IRenderedComponent<RolesControlPanel> component, TEvent message)
        where TEvent : class, IEvent
        => component.InvokeAsync(() => ((IEventConsumer<TEvent>)(object)component.Instance).Consume(
            new ClientContext<TEvent>(message, Guid.NewGuid()), Xunit.TestContext.Current.CancellationToken));

    [Fact]
    public async Task Should_add_a_created_role()
    {
        // Arrange
        await using var ctx = SetupTestContext();
        var state = CreateState("Operator");
        var component = RenderPanel(ctx, state);

        var created = new RoleCreatedEvent(new Sdk.UserManagement.Contracts.Role { Name = "Maintenance" }) { CorrelationId = Guid.NewGuid() };

        // Act
        await Deliver(component, created);

        // Assert
        state.Roles.Select(r => r.Name).Should().BeEquivalentTo("Operator", "Maintenance");
    }

    [Fact]
    public async Task Should_not_add_a_role_whose_creation_failed()
    {
        // Arrange
        await using var ctx = SetupTestContext();
        var state = CreateState("Operator");
        var component = RenderPanel(ctx, state);

        var failedCreate = new RoleCreatedEvent(new Sdk.UserManagement.Contracts.Role { Name = "Operator" })
        {
            CorrelationId = Guid.NewGuid(),
            ErrorInfo = new ErrorInfo(409, "Role already exists")
        };

        // Act
        await Deliver(component, failedCreate);

        // Assert
        state.Roles.Select(r => r.Name).Should().BeEquivalentTo("Operator");
    }

    [Fact]
    public async Task Should_remove_a_deleted_role()
    {
        // Arrange
        await using var ctx = SetupTestContext();
        var state = CreateState("Operator", "Maintenance");
        var component = RenderPanel(ctx, state);

        var deleted = new RoleDeletedEvent(new Sdk.UserManagement.Contracts.Role { Name = "Maintenance" }) { CorrelationId = Guid.NewGuid() };

        // Act
        await Deliver(component, deleted);

        // Assert
        state.Roles.Select(r => r.Name).Should().BeEquivalentTo("Operator");
    }

    [Fact]
    public async Task Should_keep_a_role_whose_deletion_failed()
    {
        // Arrange
        await using var ctx = SetupTestContext();
        var state = CreateState("Operator", "Maintenance");
        var component = RenderPanel(ctx, state);

        var failedDelete = new RoleDeletedEvent(new Sdk.UserManagement.Contracts.Role { Name = "Maintenance" })
        {
            CorrelationId = Guid.NewGuid(),
            ErrorInfo = new ErrorInfo(500, "Role is still assigned")
        };

        // Act
        await Deliver(component, failedDelete);

        // Assert
        state.Roles.Select(r => r.Name).Should().BeEquivalentTo("Operator", "Maintenance");
    }
}
