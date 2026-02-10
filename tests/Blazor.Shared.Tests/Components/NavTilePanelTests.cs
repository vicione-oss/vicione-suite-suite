using System.Text.Json;
using AngleSharp.Dom;
using AwesomeAssertions;
using Blazor.Shared.Authorization;
using Blazor.Shared.NavTiles.Components;
using Blazor.Shared.NavTiles.Extensions;
using Blazor.Shared.Tests.Mocks;
using Blazor.Shared.Tests.Models;
using Bunit;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Sdk.Authorization;
using Sdk.Authorization.Extensions;
using Sdk.Client.NavTiles.Attributes;
using Sdk.Client.NavTiles.Components;
using Sdk.Client.NavTiles.Extensions;
using Sdk.Client.NavTiles.Services;
using Sdk.Client.Services;
using Xunit;
using Xunit.Sdk;

namespace Blazor.Shared.Tests.Components;

public sealed class NavTilePanelTests
{
    public static readonly AuthorizationScenario GarplyAuthorizationScenario = new()
    {
        UserName = "Garply",
        ModuleAuthorizationClaims = [new(TestClientModuleA.ModuleId, AccessLevel.Full, TestClientModuleA.ModuleId)],
        ExpectedNavTileContents =
        [
            AdminNavTile.Content,
            UserNavTile.Content,
            AllowAnonymousNavTile.Content
        ]
    };

    public static TheoryData<AuthorizationScenario> AuthorizationScenarios =>
    [
        GarplyAuthorizationScenario,

        new AuthorizationScenario
        {
            UserName = "Fritz",
            ModuleAuthorizationClaims = [new(TestClientModuleA.ModuleId, AccessLevel.Partial, TestClientModuleA.ModuleId)],
            ExpectedNavTileContents =
            [
                UserNavTile.Content,
                AllowAnonymousNavTile.Content
            ]
        },

        new AuthorizationScenario
        {
            UserName = "Anonymous",
            ModuleAuthorizationClaims = [],
            ExpectedNavTileContents =
            [
                AllowAnonymousNavTile.Content
            ]
        }
    ];

    [Theory]
    [MemberData(nameof(AuthorizationScenarios))]
    public async Task Should_render_navigation_tiles_based_on_authorization_configuration(AuthorizationScenario authorizationScenario)
    {
        // Arrange
        await using var ctx = new BunitContext();

        ctx.Services.AddSdkAuthorization();
        ctx.Services.AddSingleton(Substitute.For<ILayoutService>());
        ctx.Services.AddSingleton<IAuthorizationHandler, ModuleAccessLevelHandler>();
        ctx.Services.AddScoped<AuthenticationStateProvider>(_ => new AuthenticationStateProviderMock(authorizationScenario.UserName, authorizationScenario.ModuleAuthorizationClaims));
        ctx.Services.AddNavTilesInfrastructure();
        ctx.Services.AddNavTiles<TestClientModuleA>();

        // Act
        var renderedComponent = ctx.Render<NavTilePanel>();

        // Assert
        AssertRenderedHtml(renderedComponent, authorizationScenario);
    }

    [Fact]
    public async Task Should_update_itself_on_authentication_state_change()
    {
        // Arrange
        await using var ctx = new BunitContext();

        ctx.Services.AddSdkAuthorization();
        ctx.Services.AddSingleton(Substitute.For<ILayoutService>());
        ctx.Services.AddSingleton<IAuthorizationHandler, ModuleAccessLevelHandler>();
        ctx.Services.AddScoped(_ => new AuthenticationStateProviderMock("Waldo"));
        ctx.Services.AddScoped<AuthenticationStateProvider>(services => services.GetRequiredService<AuthenticationStateProviderMock>());
        ctx.Services.AddNavTilesInfrastructure();
        ctx.Services.AddNavTiles<TestClientModuleA>();

        var navTileRegistry = ctx.Services.GetRequiredService<INavTileRegistry<TestClientModuleA>>();

        var renderedComponent = ctx.Render<NavTilePanel>();

        var authenticationStateProvider = ctx.Services.GetRequiredService<AuthenticationStateProviderMock>();

        // Act
        await authenticationStateProvider.ChangeUser(GarplyAuthorizationScenario.UserName, GarplyAuthorizationScenario.ModuleAuthorizationClaims);

        // Assert
        AssertRenderedHtml(renderedComponent, GarplyAuthorizationScenario);
    }

    private static void AssertRenderedHtml(IRenderedComponent<NavTilePanel> renderedComponent, AuthorizationScenario authorizationScenario)
    {
        var navTilePanelElement = renderedComponent.Find(".nav-tile-panel");
        navTilePanelElement.Should().NotBeNull();

        var navTileContainers = navTilePanelElement.QuerySelectorAll(".nav-tile-container");

        navTileContainers.Should().HaveCount(authorizationScenario.ExpectedNavTileContents.Count());

        foreach (var navTileContainer in navTileContainers)
        {
            var innerText = navTileContainer.GetInnerText();

            Assert.Contains(innerText, authorizationScenario.ExpectedNavTileContents);
        }
    }

    [InitialNavTile<TestClientModuleA>]
    [ModuleAuthorize(TestClientModuleA.ModuleId, AccessLevel.Full)]
    public partial class AdminNavTile : NavTileBase
    {
        public const string Content = "Admin";

        protected override void BuildRenderTree(RenderTreeBuilder builder)
            => builder.AddContent(1, Content);
    }

    [InitialNavTile<TestClientModuleA>]
    public partial class AllowAnonymousNavTile : NavTileBase
    {
        public const string Content = "Anonymous";

        protected override void BuildRenderTree(RenderTreeBuilder builder)
            => builder.AddContent(1, Content);
    }

    [InitialNavTile<TestClientModuleA>]
    [ModuleAuthorize(TestClientModuleA.ModuleId, AccessLevel.Partial)]
    public partial class UserNavTile : NavTileBase
    {
        public const string Content = "User";

        protected override void BuildRenderTree(RenderTreeBuilder builder)
            => builder.AddContent(1, Content);
    }

    public sealed class AuthorizationScenario : IXunitSerializable
    {
        public required string UserName { get; set; }
        public required IEnumerable<ModuleAuthorizationClaim> ModuleAuthorizationClaims { get; set; }
        public required IEnumerable<string> ExpectedNavTileContents { get; set; }

        public void Serialize(IXunitSerializationInfo info)
        {
            info.AddValue(nameof(UserName), UserName);
            info.AddValue(nameof(ModuleAuthorizationClaims), ModuleAuthorizationClaims.Select(claim => JsonSerializer.Serialize(claim)).ToArray());
            info.AddValue(nameof(ExpectedNavTileContents), ExpectedNavTileContents.ToArray());
        }

        public void Deserialize(IXunitSerializationInfo info)
        {
            UserName = info.GetValue<string>(nameof(UserName)) ?? string.Empty;
            ModuleAuthorizationClaims = (info.GetValue<string[]>(nameof(ModuleAuthorizationClaims)) ?? []).Select(claimJson => JsonSerializer.Deserialize<ModuleAuthorizationClaim>(claimJson));
            ExpectedNavTileContents = info.GetValue<string[]>(nameof(ExpectedNavTileContents)) ?? [];
        }
    }
}
