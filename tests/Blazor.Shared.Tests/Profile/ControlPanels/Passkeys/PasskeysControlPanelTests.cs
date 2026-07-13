using AwesomeAssertions;
using Blazor.Shared.Profile.ControlPanels;
using Blazor.Shared.Profile.ControlPanels.Passkeys;
using Blazor.Shared.Settings.Extensions;
using Blazor.Tests.Tools;
using Bunit;
using Core.Shared.Passkeys;
using Core.Shared.Passkeys.Requests;
using Core.Shared.UserManagement.Contracts;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.FeatureManagement;
using NSubstitute;
using Sdk.Client.ControlPanels.Extensions;
using Sdk.Client.ControlPanels.Services;
using Sdk.Client.Infrastructure;
using System.Security.Claims;
using ViciOne.Ui.Blazor.Components.Grid.Extensions;
using Xunit;

namespace Blazor.Shared.Tests.Profile.ControlPanels.Passkeys;

public sealed class PasskeysControlPanelTests
{
    private const string UserName = "TestUser";

    private readonly IFeatureManager _featureManager = Substitute.For<IFeatureManager>();
    private readonly IUiMediator _mediator = Substitute.For<IUiMediator>();
    private readonly IControlPanelRequest _controlPanelRequest = Substitute.For<IControlPanelRequest>();
    private readonly IPasskeyHostSupport _passkeyHostSupport = Substitute.For<IPasskeyHostSupport>();

    [Fact]
    public async Task Should_enable_add_passkey_when_host_is_supported()
    {
        // Arrange
        _featureManager.IsEnabledAsync(Core.Shared.Features.Constants.PasskeyFeatureName).Returns(true);
        _passkeyHostSupport.IsPasskeyCapableHost(Arg.Any<string?>()).Returns(true);

        await using var ctx = SetupTestContext();

        // Act
        var component = RenderControlPanel(ctx);

        // Assert
        component.Markup.Should().NotContain(Blazor.Shared.Profile.Localization.Passkey.RequiresDnsHost);
    }

    [Fact]
    public async Task Should_disable_add_passkey_with_tooltip_when_host_is_not_supported()
    {
        // Arrange
        _featureManager.IsEnabledAsync(Core.Shared.Features.Constants.PasskeyFeatureName).Returns(true);
        _passkeyHostSupport.IsPasskeyCapableHost(Arg.Any<string?>()).Returns(false);

        await using var ctx = SetupTestContext();

        // Act
        var component = RenderControlPanel(ctx);

        // Assert
        component.Markup.Should().Contain(Blazor.Shared.Profile.Localization.Passkey.RequiresDnsHost);
    }

    private static IRenderedComponent<PasskeysControlPanel> RenderControlPanel(BunitContext ctx)
    {
        var registry = ctx.Services.GetRequiredService<IControlPanelRegistry<SharedClientModule>>();
        var state = new PasskeysControlPanelState();
        var registryItem = registry.Add<PasskeysControlPanel, PasskeysControlPanelState>(
            new PasskeyControlPanelDescriptor(),
            state,
            new ProfileCategoryDescriptor());

        return ctx.Render<PasskeysControlPanel>(builder => builder
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
            .AddControlPanelInfrastructure()
            .AddControlPanel<SharedClientModule, PasskeysControlPanel, PasskeysControlPanelState>();

        ctx.Services
            .AddGridItemSelectColumn()
            .AddGridItemSelection<string>(typeof(PasskeyControlPanelServiceKey));

        _mediator.Request<GetPasskeys, GetPasskeysResponse>(Arg.Any<GetPasskeys>(), Arg.Any<CancellationToken>())
            .Returns(new GetPasskeysResponse([]));

        ctx.Services
            .AddSingleton(_featureManager)
            .AddSingleton(_mediator)
            .AddSingleton(_controlPanelRequest)
            .AddSingleton(_passkeyHostSupport);

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
}
