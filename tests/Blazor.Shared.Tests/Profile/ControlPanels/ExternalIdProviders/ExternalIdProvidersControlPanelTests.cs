using Blazor.Shared.Dialogs;
using Blazor.Shared.Profile.ControlPanels;
using Blazor.Shared.Profile.ControlPanels.ExternalIdProviders;
using Blazor.Shared.Services;
using Blazor.Shared.Settings.Extensions;
using Blazor.Shared.UserManagement.Contracts;
using Blazor.Shared.UserManagement.Services;
using Blazor.Tests.Tools;
using Bunit;
using Core.Shared.Security;
using Core.Shared.UserManagement.Contracts;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.JSInterop;
using Sdk.Client.ControlPanels.Extensions;
using Sdk.Client.ControlPanels.Services;
using System.Security.Claims;
using ViciOne.Ui.Blazor.Components.Dialog.Extensions;

namespace Blazor.Shared.Tests.Profile.ControlPanels.ExternalIdProviders;

public sealed class ExternalIdProvidersControlPanelTests
{
    private const string UserName = "TestUser";

    private readonly IExternalAuthenticationSettings _externalAuthenticationSettings =
        Substitute.For<IExternalAuthenticationSettings>();

    private readonly IExternalAccountService _externalAccountService = Substitute.For<IExternalAccountService>();
    private readonly INavigationService _navigationService = Substitute.For<INavigationService>();

    [Fact]
    public async Task Should_render_no_provider_configured_when_no_provider_is_configured()
    {
        // Arrange
        _externalAuthenticationSettings.IsExternalAuthenticationProviderConfigured().Returns(false);

        await using var ctx = SetupTestContext();

        // Act
        var component = RenderControlPanel(ctx, new ExternalIdProvidersControlPanelState());

        // Assert
        component.Markup.Should().Contain(Blazor.Shared.Profile.Localization.ExternalIdProviders.NoProviderConfigured);
    }

    [Fact]
    public async Task Should_render_no_linked_account_when_provider_is_configured_but_account_is_not_linked()
    {
        // Arrange
        _externalAuthenticationSettings.IsExternalAuthenticationProviderConfigured().Returns(true);
        _externalAccountService.GetExternalUserAccount(Arg.Any<SuiteUser>()).Returns((ExternalUserAccount?)null);

        await using var ctx = SetupTestContext();

        // Act
        var component = RenderControlPanel(ctx, new ExternalIdProvidersControlPanelState());

        // Assert
        component.Markup.Should().Contain(Blazor.Shared.Profile.Localization.ExternalIdProviders.NoLinkedAccount);
    }

    [Fact]
    public async Task Should_render_linked_account_display_name_when_provider_is_configured_and_account_is_linked()
    {
        // Arrange
        const string displayName = "Linked Display Name";
        _externalAuthenticationSettings.IsExternalAuthenticationProviderConfigured().Returns(true);
        _externalAccountService.GetExternalUserAccount(Arg.Any<SuiteUser>())
            .Returns(new ExternalUserAccount("OpenIdConnect", displayName, "external-key"));

        await using var ctx = SetupTestContext();

        // Act
        var component = RenderControlPanel(ctx, new ExternalIdProvidersControlPanelState());

        // Assert
        component.Markup.Should().Contain(displayName);
    }

    [Fact]
    public async Task Should_fall_back_to_login_provider_when_linked_account_has_no_display_name()
    {
        // Arrange
        const string loginProvider = "OpenIdConnect";
        _externalAuthenticationSettings.IsExternalAuthenticationProviderConfigured().Returns(true);
        _externalAccountService.GetExternalUserAccount(Arg.Any<SuiteUser>())
            .Returns(new ExternalUserAccount(loginProvider, null, "external-key"));

        await using var ctx = SetupTestContext();

        // Act
        var component = RenderControlPanel(ctx, new ExternalIdProvidersControlPanelState());

        // Assert
        component.Markup.Should().Contain(loginProvider);
    }

    [Fact]
    public async Task Should_redirect_to_login_with_return_url_after_successful_unlink()
    {
        // Arrange
        _externalAuthenticationSettings.IsExternalAuthenticationProviderConfigured().Returns(true);
        _externalAccountService.GetExternalUserAccount(Arg.Any<SuiteUser>())
            .Returns(new ExternalUserAccount("OpenIdConnect", "Test User", "external-key"));
        _externalAccountService
            .RemoveExternalAccount(Arg.Any<SuiteUser>(),
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IUserManagementServiceResult>(new UserManagementServiceSuccessResult()));

        var capturingNavManager = new CapturingNavigationManager();
        _navigationService.NavManager.Returns(capturingNavManager);

        await using var ctx = SetupTestContext();

        var component = RenderControlPanel(ctx, new ExternalIdProvidersControlPanelState());
        var dialog = component.FindComponent<ConfirmCancelDialog>();

        // Act
        await component.InvokeAsync(async () => await dialog.Instance.OnConfirm.InvokeAsync());

        // Assert
        capturingNavManager.LastNavigatedUri.Should().Be("/account/login?returnUrl=%2F");
        capturingNavManager.LastForceLoad.Should().BeTrue();
    }

    [Fact]
    public async Task Should_display_error_and_not_redirect_when_unlink_fails()
    {
        // Arrange
        const string errorMessage = "Cannot remove the last login method";
        _externalAuthenticationSettings.IsExternalAuthenticationProviderConfigured().Returns(true);
        _externalAccountService.GetExternalUserAccount(Arg.Any<SuiteUser>())
            .Returns(new ExternalUserAccount("OpenIdConnect", "Test User", "external-key"));
        _externalAccountService
            .RemoveExternalAccount(Arg.Any<SuiteUser>(),
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IUserManagementServiceResult>(new UserManagementServiceErrorResult(errorMessage)));

        var capturingNavManager = new CapturingNavigationManager();
        _navigationService.NavManager.Returns(capturingNavManager);

        await using var ctx = SetupTestContext();

        var component = RenderControlPanel(ctx, new ExternalIdProvidersControlPanelState());
        var dialog = component.FindComponent<ConfirmCancelDialog>();

        // Act
        await component.InvokeAsync(async () => await dialog.Instance.OnConfirm.InvokeAsync());

        // Assert
        capturingNavManager.LastNavigatedUri.Should().BeNull();
        component.Markup.Should().Contain(errorMessage);
    }

    private static IRenderedComponent<ExternalIdProvidersControlPanel> RenderControlPanel(
        BunitContext ctx,
        ExternalIdProvidersControlPanelState state)
    {
        var registry = ctx.Services.GetRequiredService<IControlPanelRegistry<SharedClientModule>>();
        var registryItem = registry.Add<ExternalIdProvidersControlPanel, ExternalIdProvidersControlPanelState>(
            new ExternalIdProvidersControlPanelDescriptor(),
            state,
            new ProfileCategoryDescriptor());

        return ctx.Render<ExternalIdProvidersControlPanel>(builder => builder
            .Add(c => c.State, state)
            .AddCascadingValue(registryItem));
    }

    private BunitContext SetupTestContext()
    {
        var ctx = new BunitContext();

        ctx.SetupSuiteServicesWithBlazorDx()
            .AddAuthorization()
            .SetAuthorized(UserName);

        ctx.Services
            .AddDialog()
            .AddControlPanelInfrastructure()
            .AddControlPanel<SharedClientModule, ExternalIdProvidersControlPanel,
                ExternalIdProvidersControlPanelState>();

        ctx.Services
            .AddSingleton(_externalAuthenticationSettings)
            .AddSingleton(_externalAccountService)
            .AddSingleton(_navigationService)
            .AddSingleton(Substitute.For<ILogger<AddExternalIdProviderButton>>())
            .AddSingleton(Substitute.For<IJSRuntime>());

        var userManager = Substitute.For<UserManager<SuiteUser>>(
            Substitute.For<IUserStore<SuiteUser>>(),
            Substitute.For<IOptions<IdentityOptions>>(),
            Substitute.For<IPasswordHasher<SuiteUser>>(),
            Substitute.For<IEnumerable<IUserValidator<SuiteUser>>>(),
            Substitute.For<IEnumerable<IPasswordValidator<SuiteUser>>>(),
            Substitute.For<ILookupNormalizer>(),
            Substitute.For<IdentityErrorDescriber>(),
            Substitute.For<IServiceProvider>(),
            Substitute.For<ILogger<UserManager<SuiteUser>>>());
        userManager.GetUserAsync(Arg.Any<ClaimsPrincipal>()).Returns(new SuiteUser { UserName = UserName });
        ctx.Services.AddSingleton(userManager);

        return ctx;
    }

    private sealed class CapturingNavigationManager : NavigationManager
    {
        public string? LastNavigatedUri { get; private set; }
        public bool LastForceLoad { get; private set; }

        public CapturingNavigationManager() => Initialize("http://localhost:2112/", "http://localhost:2112/");

        protected override void NavigateToCore(string uri, NavigationOptions options)
        {
            LastNavigatedUri = uri;
            LastForceLoad = options.ForceLoad;
        }
    }
}
