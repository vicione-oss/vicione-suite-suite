using AwesomeAssertions;
using Blazor.Shared.Settings.Extensions;
using Blazor.Shared.UserManagement.ControlPanels.User.Components;
using Blazor.Shared.UserManagement.ControlPanels.User.Extensions;
using Blazor.Shared.UserManagement.ControlPanels.User.Services;
using Blazor.Shared.UserManagement.Services;
using Blazor.Tests.Tools;
using Bunit;
using Core.Shared.UserManagement.Contracts;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Sdk.Authorization;
using Sdk.Client.ControlPanels.Services;
using Sdk.Client.Infrastructure;
using Sdk.Messaging;
using Sdk.Testing.Client;
using Sdk.UserManagement.Events;
using ViciOne.Ui.Blazor.Components.TestingHelpers.TextBox.Extensions;
using Xunit;

namespace Blazor.Shared.Tests.UserManagement.ControlPanels.User.Components;

public sealed class UserControlPanelTests
{
    private readonly IRoleService _roleService = Substitute.For<IRoleService>();

    private BunitContext SetupTestContext()
    {
        var ctx = new BunitContext();

        ctx.JSInterop.SetupForTextBox();
        ctx.JSInterop.ConfigureQuickGridJSInterop();
        ctx.AddAuthorization().SetAuthorized("admin");

        _roleService.GetAvailableRoles(Arg.Any<CancellationToken>())
            .Returns([new Sdk.UserManagement.Contracts.Role { Name = "Operator" }, new Sdk.UserManagement.Contracts.Role { Name = "Maintenance" }]);

        ctx.SetupSuiteServices(setup =>
        {
            setup.Services.AddControlPanelInfrastructure()
                .AddUserControlPanel()
                .AddTransient(_ => _roleService)
                .AddTransient(_ => Substitute.For<IModuleAuthorizationClaimParser>());
        });

        return ctx;
    }

    private static IRenderedComponent<UserControlPanel> RenderPanel(BunitContext ctx, UserControlPanelState state)
    {
        var controlPanelRegistry = ctx.Services.GetRequiredService<IControlPanelRegistry<SharedClientModule>>().First();

        return ctx.Render<UserControlPanel>(builder => builder.Add(c => c.State, state).AddCascadingValue(controlPanelRegistry));
    }

    private static UserControlPanelState CreateStateForUserWithRoles(params string[] roles)
        => new()
        {
            UserName = new UserName("operator"),
            UserProfile = new UserProfile { UserName = new UserName("operator"), Roles = [.. roles] }
        };

    private static Task DeliverRoleDeleted(IRenderedComponent<UserControlPanel> component, RoleDeletedEvent message)
        => component.InvokeAsync(() => component.Instance.Consume(
            new ClientContext<RoleDeletedEvent>(message, Guid.NewGuid()), Xunit.TestContext.Current.CancellationToken));

    [Fact]
    public async Task Should_remove_a_deleted_role_from_the_user()
    {
        // Arrange
        await using var ctx = SetupTestContext();
        var state = CreateStateForUserWithRoles("Operator", "Maintenance");
        var component = RenderPanel(ctx, state);

        var deleted = new RoleDeletedEvent(new Sdk.UserManagement.Contracts.Role { Name = "Maintenance" }) { CorrelationId = Guid.NewGuid() };

        // Act
        await DeliverRoleDeleted(component, deleted);

        // Assert
        state.UserProfile!.Roles.Should().BeEquivalentTo("Operator");
    }

    [Fact]
    public async Task Should_keep_a_role_whose_deletion_failed()
    {
        // Arrange
        await using var ctx = SetupTestContext();
        var state = CreateStateForUserWithRoles("Operator", "Maintenance");
        var component = RenderPanel(ctx, state);

        var failedDelete = new RoleDeletedEvent(new Sdk.UserManagement.Contracts.Role { Name = "Maintenance" })
        {
            CorrelationId = Guid.NewGuid(),
            ErrorInfo = new ErrorInfo(500, "Role is still assigned")
        };

        // Act
        await DeliverRoleDeleted(component, failedDelete);

        // Assert
        state.UserProfile!.Roles.Should().BeEquivalentTo("Operator", "Maintenance");
    }
}
