using System.Globalization;
using AwesomeAssertions;
using Blazor.Shared.Settings.DateAndTime.Services;
using Blazor.Shared.Settings.Extensions;
using Blazor.Shared.UserInterface.ControlPanels.DateAndTime.Models;
using Blazor.Shared.UserManagement.ControlPanels.User.Components;
using Blazor.Shared.UserManagement.ControlPanels.User.Extensions;
using Blazor.Shared.UserManagement.ControlPanels.User.Services;
using Blazor.Shared.UserManagement.Services;
using Blazor.Tests.Tools;
using Bunit;
using Core.Shared.Instance.Contracts;
using Core.Shared.Instance.Requests;
using Core.Shared.UserManagement.Contracts;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Sdk.Authorization;
using Sdk.Client.Components.Settings;
using Sdk.Client.ControlPanels.Services;
using Sdk.Client.Infrastructure;
using Sdk.Messaging;
using Sdk.Testing.Client;
using Sdk.UserManagement.Events;
using ViciOne.Ui.Blazor.Components.ComboBox;
using ViciOne.Ui.Blazor.Components.TestingHelpers.TextBox.Extensions;
using ViciOne.Ui.Localization.Resources;
using Xunit;

namespace Blazor.Shared.Tests.UserManagement.ControlPanels.User.Components;

public sealed class UserControlPanelTests
{
    private const string BerlinTimeZoneId = "Europe/Berlin";
    private const string NewYorkTimeZoneId = "America/New_York";

    private static readonly CultureInfo German = CultureInfo.GetCultureInfo("de-DE");

    private readonly IRoleService _roleService = Substitute.For<IRoleService>();
    private readonly ITimeZoneDescriptorProvider _timeZoneDescriptorProvider = Substitute.For<ITimeZoneDescriptorProvider>();

    private BunitContext SetupTestContext(bool cancelTimeZoneLoading = false)
    {
        var ctx = new BunitContext();

        ctx.JSInterop.SetupForTextBox();
        ctx.JSInterop.ConfigureQuickGridJSInterop();
        ctx.AddAuthorization().SetAuthorized("admin");

        _roleService.GetAvailableRoles(Arg.Any<CancellationToken>())
            .Returns([new Sdk.UserManagement.Contracts.Role { Name = "Operator" }, new Sdk.UserManagement.Contracts.Role { Name = "Maintenance" }]);

        // Returned out of UTC offset order, so the panel's ordering is observable.
        _timeZoneDescriptorProvider.GetAll(Arg.Any<CancellationToken>()).Returns(cancelTimeZoneLoading
            ? Task.FromCanceled<IEnumerable<TimeZoneDescriptor>>(new CancellationToken(canceled: true))
            : Task.FromResult<IEnumerable<TimeZoneDescriptor>>(
            [
                CreateTimeZoneDescriptor(BerlinTimeZoneId, "Berlin"),
                CreateTimeZoneDescriptor(NewYorkTimeZoneId, "New York")
            ]));

        ctx.SetupSuiteServices(setup =>
        {
            setup.Services.AddControlPanelInfrastructure()
                .AddUserControlPanel()
                .AddTransient(_ => _roleService)
                .AddTransient(_ => _timeZoneDescriptorProvider)
                .AddTransient(_ => Substitute.For<IModuleAuthorizationClaimParser>());

            setup.ClientMediator.Request<GetCrossInstanceConfiguration, GetCrossInstanceConfigurationResponse>(
                    Arg.Any<GetCrossInstanceConfiguration>(), Arg.Any<CancellationToken>())
                .Returns(new GetCrossInstanceConfigurationResponse(new CrossInstanceConfiguration { CultureName = German.Name }));
        });

        return ctx;
    }

    private static TimeZoneDescriptor CreateTimeZoneDescriptor(string timeZoneId, string displayName)
        => new(timeZoneId, displayName, TimeZoneInfo.FindSystemTimeZoneById(timeZoneId).BaseUtcOffset,
            new Uri("https://example.com/time-zone.svg"));

    private static IRenderedComponent<UserControlPanel> RenderPanel(BunitContext ctx,
        UserControlPanelState state,
        Action? onBeginEdit = null)
    {
        var controlPanelRegistry = ctx.Services.GetRequiredService<IControlPanelRegistry<SharedClientModule>>().First();

        return ctx.Render<UserControlPanel>(builder =>
        {
            builder.Add(c => c.State, state)
                .AddCascadingValue(controlPanelRegistry);

            if (onBeginEdit is not null)
                builder.Add(c => c.OnBeginEdit, onBeginEdit);
        });
    }

    private static UserControlPanelState CreateStateForUserWithRoles(params string[] roles)
        => new()
        {
            UserName = new UserName("operator"),
            UserProfile = new UserProfile { UserName = new UserName("operator"), Roles = [.. roles] }
        };

    private static UserControlPanelState CreateState(CultureInfo? selectedCulture = null, TimeZoneInfo? selectedTimeZone = null)
    {
        var state = CreateStateForUserWithRoles();
        state.SelectedCulture = selectedCulture;
        state.SelectedTimeZone = selectedTimeZone;

        return state;
    }

    private static Task DeliverRoleDeleted(IRenderedComponent<UserControlPanel> component, RoleDeletedEvent message)
        => component.InvokeAsync(() => component.Instance.Consume(
            new ClientContext<RoleDeletedEvent>(message, Guid.NewGuid()), Xunit.TestContext.Current.CancellationToken));

    private static IRenderedComponent<SettingsFieldComboBox<ComboBoxItem<CultureInfo?, string>, CultureInfo?>> GetLanguageComboBox(
        IRenderedComponent<UserControlPanel> component)
        => component.GetSettingsFieldComboBox<ComboBoxItem<CultureInfo?, string>, CultureInfo?>(CommonVocabulary.Language);

    private static IRenderedComponent<SettingsFieldComboBox<ComboBoxItem<TimeZoneInfo?, string>, TimeZoneInfo?>> GetTimeZoneComboBox(
        IRenderedComponent<UserControlPanel> component)
        => component.GetSettingsFieldComboBox<ComboBoxItem<TimeZoneInfo?, string>, TimeZoneInfo?>(CommonVocabulary.TimeZone);

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

    [Fact]
    public async Task Should_offer_default_and_supported_cultures()
    {
        // Arrange
        await using var ctx = SetupTestContext();

        // Act
        var items = GetLanguageComboBox(RenderPanel(ctx, CreateState())).Instance.Items.ToList();

        // Assert
        items[0].Text.Should().Be($"{CommonVocabulary.Default} - {German.DisplayName}");
        items.Select(i => i.Value).Should().Equal([null, .. CrossInstanceConfiguration.SupportedCultures]);
    }

    [Fact]
    public async Task Should_select_default_culture_when_user_has_no_language()
    {
        // Arrange
        await using var ctx = SetupTestContext();

        // Act
        var component = RenderPanel(ctx, CreateState());

        // Assert
        component.AssertSettingsFieldComboBoxWithItem<CultureInfo?>(CommonVocabulary.Language, null);
    }

    [Fact]
    public async Task Should_select_users_culture()
    {
        // Arrange
        await using var ctx = SetupTestContext();

        // Act
        var component = RenderPanel(ctx, CreateState(selectedCulture: German));

        // Assert
        component.AssertSettingsFieldComboBoxWithItem<CultureInfo?>(CommonVocabulary.Language, German);
    }

    [Fact]
    public async Task Should_offer_default_and_time_zones_ordered_by_offset()
    {
        // Arrange
        await using var ctx = SetupTestContext();

        // Act
        var items = GetTimeZoneComboBox(RenderPanel(ctx, CreateState())).Instance.Items.ToList();

        // Assert
        items.Select(i => i.Text).Should().Equal(CommonVocabulary.Default, "New York", "Berlin");
        items.Select(i => i.Value?.Id).Should().Equal(null, NewYorkTimeZoneId, BerlinTimeZoneId);
    }

    [Fact]
    public async Task Should_select_users_time_zone()
    {
        // Arrange
        var berlin = TimeZoneInfo.FindSystemTimeZoneById(BerlinTimeZoneId);

        await using var ctx = SetupTestContext();

        // Act
        var component = RenderPanel(ctx, CreateState(selectedTimeZone: berlin));

        // Assert
        component.AssertSettingsFieldComboBoxWithItem<TimeZoneInfo?>(CommonVocabulary.TimeZone, berlin);
    }

    [Fact]
    public async Task Should_render_when_time_zone_loading_is_cancelled()
    {
        // Arrange
        await using var ctx = SetupTestContext(cancelTimeZoneLoading: true);

        // Act
        var items = GetTimeZoneComboBox(RenderPanel(ctx, CreateState())).Instance.Items.ToList();

        // Assert
        items.Select(i => i.Value).Should().Equal([null]);
    }

    [Fact]
    public async Task Should_update_culture_and_begin_edit_on_selection()
    {
        // Arrange
        var state = CreateState();

        await using var ctx = SetupTestContext();

        var beginEditRaised = false;
        var comboBox = GetLanguageComboBox(RenderPanel(ctx, state, () => beginEditRaised = true));

        // Act
        await comboBox.InvokeAsync(() => comboBox.Instance.ValueChanged.InvokeAsync(German));

        // Assert
        state.SelectedCulture.Should().Be(German);
        beginEditRaised.Should().BeTrue();
    }

    [Fact]
    public async Task Should_update_time_zone_and_begin_edit_on_selection()
    {
        // Arrange
        var state = CreateState();
        var berlin = TimeZoneInfo.FindSystemTimeZoneById(BerlinTimeZoneId);

        await using var ctx = SetupTestContext();

        var beginEditRaised = false;
        var comboBox = GetTimeZoneComboBox(RenderPanel(ctx, state, () => beginEditRaised = true));

        // Act
        await comboBox.InvokeAsync(() => comboBox.Instance.ValueChanged.InvokeAsync(berlin));

        // Assert
        state.SelectedTimeZone.Should().Be(berlin);
        beginEditRaised.Should().BeTrue();
    }
}
