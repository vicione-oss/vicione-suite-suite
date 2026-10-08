using AwesomeAssertions;
using Blazor.Shared.Settings.Extensions;
using Blazor.Shared.UserManagement.ControlPanels.Role.Components;
using Blazor.Shared.UserManagement.ControlPanels.Role.Extensions;
using Blazor.Shared.UserManagement.ControlPanels.Role.Services;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Sdk.Client.ControlPanels.Services;
using Sdk.Client.Infrastructure;
using Sdk.Messaging;
using Sdk.Testing.Client;
using Sdk.UserManagement.Events;
using ViciOne.Ui.Blazor.Components.TestingHelpers.TextBox.Extensions;
using Xunit;

namespace Blazor.Shared.Tests.UserManagement.ControlPanels.Role.Components;

public sealed class RoleControlPanelTests
{
    private readonly IAvailableClaimsService _availableClaimsService = Substitute.For<IAvailableClaimsService>();

    private BunitContext SetupTestContext()
    {
        var ctx = new BunitContext();

        ctx.JSInterop.SetupForTextBox();

        ctx.SetupSuiteServices(setup =>
        {
            setup.Services.AddControlPanelInfrastructure()
                .AddRoleControlPanel()
                .AddTransient(_ => _availableClaimsService)
                .AddTransient(_ => Substitute.For<IGridItemService>());
        });

        return ctx;
    }

    private static IRenderedComponent<RoleControlPanel> RenderPanel(BunitContext ctx, RoleControlPanelState state)
    {
        var controlPanelRegistry = ctx.Services.GetRequiredService<IControlPanelRegistry<SharedClientModule>>().First();

        return ctx.Render<RoleControlPanel>(builder => builder.Add(c => c.State, state).AddCascadingValue(controlPanelRegistry));
    }

    private static RoleControlPanelState CreateStateInCreateMode()
        => new() { Role = new Sdk.UserManagement.Contracts.Role { Name = "Operator" } };

    [Fact]
    public async Task Should_refresh_but_stay_in_create_mode_when_a_role_is_created_elsewhere()
    {
        // Arrange
        await using var ctx = SetupTestContext();
        var state = CreateStateInCreateMode();
        var component = RenderPanel(ctx, state);

        var createdElsewhere = new RoleCreatedEvent(new Sdk.UserManagement.Contracts.Role { Name = "Maintenance" }) { CorrelationId = Guid.NewGuid() };

        // Act
        await component.InvokeAsync(() => component.Instance.Consume(
            new ClientContext<RoleCreatedEvent>(createdElsewhere, Guid.NewGuid()), Xunit.TestContext.Current.CancellationToken));

        // Assert
        state.RoleName.Should().BeNull();
        state.IsEditMode.Should().BeFalse();
        await _availableClaimsService.Received(1).UpdateAvailableClaims(state);
    }

    [Fact]
    public async Task Should_ignore_a_role_event_that_reports_an_error()
    {
        // Arrange
        await using var ctx = SetupTestContext();
        var state = CreateStateInCreateMode();
        var component = RenderPanel(ctx, state);

        var failedCreate = new RoleCreatedEvent(state.Role!) { CorrelationId = Guid.NewGuid(), ErrorInfo = new ErrorInfo(409, "Role already exists") };

        // Act
        await component.InvokeAsync(() => component.Instance.Consume(
            new ClientContext<RoleCreatedEvent>(failedCreate, Guid.NewGuid()), Xunit.TestContext.Current.CancellationToken));

        // Assert
        state.RoleName.Should().BeNull();
        state.IsEditMode.Should().BeFalse();
        await _availableClaimsService.DidNotReceive().UpdateAvailableClaims(Arg.Any<RoleControlPanelState>());
    }
}
