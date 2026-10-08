using AwesomeAssertions;
using Blazor.Shared.UserManagement.ControlPanels.Role.Services;
using Blazor.Shared.UserManagement.Contracts;
using Blazor.Shared.UserManagement.Services;
using NSubstitute;
using Sdk.Client.ControlPanels.Models;
using Xunit;

namespace Blazor.Shared.Tests.UserManagement.ControlPanels.Role.Services;

public sealed class RoleControlPanelSaveHandlerTests
{
    private readonly IRoleService _roleService = Substitute.For<IRoleService>();

    private RoleControlPanelSaveHandler CreateSaveHandler()
        => new(_roleService);

    private static RoleControlPanelState CreateStateInCreateMode(string roleName)
        => new() { Role = new Sdk.UserManagement.Contracts.Role { Name = roleName } };

    private static RoleControlPanelState CreateStateInEditMode(string roleName)
        => new() { RoleName = roleName, Role = new Sdk.UserManagement.Contracts.Role { Name = roleName } };

    [Fact]
    public async Task Should_create_the_role_and_switch_to_edit_mode_on_success()
    {
        // Arrange
        var state = CreateStateInCreateMode("Operator");
        _roleService.CreateRole(state.Role!, Arg.Any<CancellationToken>())
            .Returns(new UserManagementServiceSuccessResult());

        // Act
        var result = await CreateSaveHandler().Save(state, TestContext.Current.CancellationToken);

        // Assert
        result.Should().BeOfType<SaveSuccessResult>();
        state.RoleName.Should().Be("Operator");
        state.IsEditMode.Should().BeTrue();
        await _roleService.DidNotReceive().UpdateRole(Arg.Any<Sdk.UserManagement.Contracts.Role>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Should_stay_in_create_mode_when_creating_the_role_fails()
    {
        // Arrange
        var state = CreateStateInCreateMode("Administrator");
        _roleService.CreateRole(state.Role!, Arg.Any<CancellationToken>())
            .Returns(new UserManagementServiceErrorResult("Role already exists"));

        // Act
        var result = await CreateSaveHandler().Save(state, TestContext.Current.CancellationToken);

        // Assert
        result.Should().BeOfType<SaveErrorResult>()
            .Which.Message.Should().Be("Role already exists");
        state.RoleName.Should().BeNull();
        state.IsEditMode.Should().BeFalse();
    }

    [Fact]
    public async Task Should_update_the_role_when_in_edit_mode()
    {
        // Arrange
        var state = CreateStateInEditMode("Operator");
        _roleService.UpdateRole(state.Role!, Arg.Any<CancellationToken>())
            .Returns(new UserManagementServiceSuccessResult());

        // Act
        var result = await CreateSaveHandler().Save(state, TestContext.Current.CancellationToken);

        // Assert
        result.Should().BeOfType<SaveSuccessResult>();
        state.RoleName.Should().Be("Operator");
        await _roleService.DidNotReceive().CreateRole(Arg.Any<Sdk.UserManagement.Contracts.Role>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Should_throw_when_no_role_is_provided()
    {
        // Arrange
        var state = new RoleControlPanelState();

        // Act
        var act = () => CreateSaveHandler().Save(state, TestContext.Current.CancellationToken);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>();
    }
}
