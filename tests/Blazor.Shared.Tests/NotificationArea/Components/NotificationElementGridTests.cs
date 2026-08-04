using System.Text.Json;
using Blazor.Shared.Authorization;
using Blazor.Shared.NotificationArea.Components;
using Blazor.Shared.NotificationArea.Extensions;
using Blazor.Shared.Tests.Mocks;
using Blazor.Shared.Tests.Models;
using Bunit;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Sdk.Authorization;
using Sdk.Authorization.Extensions;
using Sdk.Client.NotificationArea.Attributes;
using Sdk.Client.NotificationArea.Components;
using Sdk.Client.NotificationArea.Extensions;
using Sdk.Client.NotificationArea.Services;
using Sdk.Client.Services;
using Xunit.Sdk;

namespace Blazor.Shared.Tests.NotificationArea.Components;

public sealed class NotificationElementGridTests
{
    public static readonly AuthorizationScenario FirstAuthorizationScenario = new()
    {
        UserName = "Hans",
        ModuleAuthorizationClaims =
        [
            new(TestClientModuleA.ModuleId, AccessLevel.Full, TestClientModuleA.ModuleId),
            new(TestClientModuleB.ModuleId, AccessLevel.Full, TestClientModuleB.ModuleId)
        ],
        ExpectedNotificationElementTitles =
        [
            NotificationElementA.Title,
            NotificationElementB.Title,
            NotificationElementC.Title
        ]
    };

    public static readonly AuthorizationScenario SecondAuthorizationScenario = new()
    {
        UserName = "Fritz",
        ModuleAuthorizationClaims =
        [
            new(TestClientModuleA.ModuleId, AccessLevel.Partial, TestClientModuleA.ModuleId),
            new(TestClientModuleB.ModuleId, AccessLevel.Partial, TestClientModuleB.ModuleId)
        ],
        ExpectedNotificationElementTitles =
        [
            NotificationElementA.Title,
            NotificationElementB.Title
        ]
    };

    public static TheoryData<AuthorizationScenario> AuthorizationScenarios =>
        [
            FirstAuthorizationScenario,
            SecondAuthorizationScenario,

            new AuthorizationScenario
            {
                UserName = "Anonymous",
                ModuleAuthorizationClaims = [],
                ExpectedNotificationElementTitles =
                [
                    NotificationElementA.Title
                ]
            }
        ];

    private static BunitContext SetupTestContext(AuthenticationStateProvider provider)
    {
        var ctx = new BunitContext();

        ctx.Services.AddSdkAuthorization();
        ctx.Services.AddSingleton(Substitute.For<ILayoutService>());
        ctx.Services.AddSingleton<IAuthorizationHandler, ModuleAccessLevelHandler>();
        ctx.Services.AddScoped<AuthenticationStateProvider>(_ => provider);
        ctx.Services.AddNotificationArea();

        return ctx;
    }

    [Fact]
    public void Should_render_component()
    {
        // Arrange
        using var ctx = SetupTestContext(new AuthenticationStateProviderMock("Garply"));

        // Act
        var renderedComponent = ctx.Render<NotificationElementGrid>();

        // Assert
        renderedComponent.Should().NotBeNull();
    }

    [Fact]
    public void Should_display_notification_elements()
    {
        // Arrange
        using var ctx = SetupTestContext(new AuthenticationStateProviderMock(FirstAuthorizationScenario.UserName, FirstAuthorizationScenario.ModuleAuthorizationClaims));

        ctx.Services.AddNotificationElements<TestClientModuleA>();
        ctx.Services.AddNotificationElements<TestClientModuleB>();

        // Act
        var renderedComponent = ctx.Render<NotificationElementGrid>();

        // Assert
        var notificationElements = renderedComponent.FindAll(".notification-element");
        notificationElements.Should().NotBeNull().And.HaveCount(3);
    }

    [Fact]
    public void Should_display_first_notification_element_as_active()
    {
        // Arrange
        using var ctx = SetupTestContext(new AuthenticationStateProviderMock("Garply"));

        ctx.Services.AddNotificationElements<TestClientModuleA>();
        ctx.Services.AddNotificationElements<TestClientModuleB>();

        // Act
        var renderedComponent = ctx.Render<NotificationElementGrid>();

        var registry = ctx.Services.GetRequiredService<INotificationElementRegistry<TestClientModuleA>>();
        var firstNotificationElement = registry.First();
        firstNotificationElement.State.IsActive = true;

        renderedComponent.Render();

        // Assert
        var activeNotificationElements = renderedComponent.FindAll(".notification-element--active");
        activeNotificationElements.Should().NotBeNull().And.HaveCount(1);

        var firstActiveNotificationElement = activeNotificationElements[0];
        firstActiveNotificationElement.Children.First().GetAttribute("title").Should().Be(NotificationElementA.Title);
    }

    [Fact]
    public void Should_have_single_active_notification_element_only()
    {
        // Arrange
        using var ctx = SetupTestContext(new AuthenticationStateProviderMock(FirstAuthorizationScenario.UserName, FirstAuthorizationScenario.ModuleAuthorizationClaims));

        ctx.Services.AddNotificationElements<TestClientModuleA>();
        ctx.Services.AddNotificationElements<TestClientModuleB>();

        var renderedComponent = ctx.Render<NotificationElementGrid>();

        // Act + Assert
        var registries = ctx.Services.GetRequiredService<IEnumerable<INotificationElementRegistry>>();

        var registryItems = registries.SelectMany(registry => registry).ToList();

        foreach (var registryItem in registryItems)
        {
            registryItem.State.IsActive = true;

            renderedComponent.Render();

            var activeNotificationElements = renderedComponent.FindAll(".notification-element--active");
            activeNotificationElements.Should().NotBeNull().And.HaveCount(1);

            var firstActiveNotificationElement = activeNotificationElements[0];
            var requiredTitle = registryItem.Id.ToString() switch
            {
                NotificationElementA.DefaultId => NotificationElementA.Title,
                NotificationElementB.DefaultId => NotificationElementB.Title,
                NotificationElementC.DefaultId => NotificationElementC.Title,
                _ => "?",
            };

            firstActiveNotificationElement.Children.First().GetAttribute("title").Should().Be(requiredTitle);
        }
    }

    [Fact]
    public void Should_reflect_registry_add()
    {
        // Arrange
        using var ctx = SetupTestContext(new AuthenticationStateProviderMock("Garply"));

        ctx.Services.AddNotificationElements<TestClientModuleB>();

        var renderedComponent = ctx.Render<NotificationElementGrid>();

        // Act
        var registry = ctx.Services.GetRequiredService<INotificationElementRegistry<TestClientModuleB>>();
        registry.Add<NotificationElementD, NotificationElementState>(new NotificationElementState());

        renderedComponent.Render();

        // Assert
        var notificationElements = renderedComponent.FindAll(".notification-element");
        notificationElements.Should().Contain(
            notificationElement => notificationElement.Children.First().GetAttribute("title") == NotificationElementD.Title);
    }

    [Fact]
    public void Should_reflect_registry_remove()
    {
        // Arrange
        using var ctx = SetupTestContext(new AuthenticationStateProviderMock("Garply"));

        ctx.Services.AddNotificationElements<TestClientModuleA>();

        var renderedComponent = ctx.Render<NotificationElementGrid>();

        // Act
        var registry = ctx.Services.GetRequiredService<INotificationElementRegistry<TestClientModuleA>>();

        var notificationElementId = new Guid(NotificationElementA.DefaultId);
        registry.Remove(notificationElementId);

        renderedComponent.Render();

        // Assert
        var notificationElements = renderedComponent.FindAll(".notification-element");
        notificationElements.Should().NotContain(
            notificationElement => notificationElement.Children.First().GetAttribute("title") == NotificationElementA.Title);
    }

    [Theory]
    [MemberData(nameof(AuthorizationScenarios))]
    public void Should_render_notification_elements_based_on_authorization_configuration(AuthorizationScenario authorizationScenario)
    {
        // Arrange
        using var ctx = SetupTestContext(new AuthenticationStateProviderMock(authorizationScenario.UserName, authorizationScenario.ModuleAuthorizationClaims));

        ctx.Services.AddNotificationElements<TestClientModuleA>();
        ctx.Services.AddNotificationElements<TestClientModuleB>();

        // Act
        var renderedComponent = ctx.Render<NotificationElementGrid>();

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
        ctx.Services.AddScoped(_ => new AuthenticationStateProviderMock(FirstAuthorizationScenario.UserName, FirstAuthorizationScenario.ModuleAuthorizationClaims));
        ctx.Services.AddScoped<AuthenticationStateProvider>(services => services.GetRequiredService<AuthenticationStateProviderMock>());
        ctx.Services.AddNotificationArea();
        ctx.Services.AddNotificationElements<TestClientModuleA>();
        ctx.Services.AddNotificationElements<TestClientModuleB>();

        var renderedComponent = ctx.Render<NotificationElementGrid>();

        var authenticationStateProvider = ctx.Services.GetRequiredService<AuthenticationStateProviderMock>();

        // Act
        await authenticationStateProvider.ChangeUser(SecondAuthorizationScenario.UserName, SecondAuthorizationScenario.ModuleAuthorizationClaims);

        // Assert
        AssertRenderedHtml(renderedComponent, SecondAuthorizationScenario);
    }

    private static void AssertRenderedHtml(IRenderedComponent<NotificationElementGrid> renderedComponent, AuthorizationScenario authorizationScenario)
    {
        var notificationElements = renderedComponent.FindAll(".notification-element");

        notificationElements.Should().HaveCount(authorizationScenario.ExpectedNotificationElementTitles.Count());

        foreach (var notificationElement in notificationElements)
        {
            var titleElement = notificationElement.QuerySelector("[title]");
            Assert.NotNull(titleElement);
            var title = titleElement.GetAttribute("title");

            authorizationScenario.ExpectedNotificationElementTitles.Should().Contain(title);
        }
    }

    public sealed class NotificationElementIcon : ComponentBase;

    [InitialNotificationElement<TestClientModuleA>(Id = DefaultId)]
    public sealed class NotificationElementA : NotificationElement<NotificationElementState, NotificationElementIcon>
    {
        internal const string DefaultId = "6ae46729-910f-46f2-aac3-0b158d702160";

        internal const string Title = "Notification element A";

        protected override string GetTitle() => Title;
    }

    [InitialNotificationElement<TestClientModuleA>(Id = DefaultId)]
    [ModuleAuthorize(TestClientModuleA.ModuleId, AccessLevel.Partial)]
    public sealed class NotificationElementB : NotificationElement<NotificationElementState, NotificationElementIcon>
    {
        internal const string DefaultId = "3b3cb996-0477-4004-9713-7f6fc527c751";

        internal const string Title = "Notification element B";

        protected override string GetTitle() => Title;
    }

    [InitialNotificationElement<TestClientModuleB>(Id = DefaultId)]
    [ModuleAuthorize(TestClientModuleB.ModuleId, AccessLevel.Full)]
    public sealed class NotificationElementC : NotificationElement<NotificationElementState, NotificationElementIcon>
    {
        internal const string DefaultId = "235c3bf2-2a30-4b52-89e1-56c9b73385b0";

        internal const string Title = "Notification element C";

        protected override string GetTitle() => Title;
    }

    public sealed class NotificationElementD : NotificationElement<NotificationElementState, NotificationElementIcon>
    {
        internal const string Title = "Notification element D";

        protected override string GetTitle() => Title;
    }

    public sealed class AuthorizationScenario : IXunitSerializable
    {
        public required string UserName { get; set; }
        public required IEnumerable<ModuleAuthorizationClaim> ModuleAuthorizationClaims { get; set; }
        public required IEnumerable<string> ExpectedNotificationElementTitles { get; set; }

        public void Serialize(IXunitSerializationInfo info)
        {
            info.AddValue(nameof(UserName), UserName);
            info.AddValue(nameof(ModuleAuthorizationClaims), ModuleAuthorizationClaims.Select(claim => JsonSerializer.Serialize(claim)).ToArray());
            info.AddValue(nameof(ExpectedNotificationElementTitles), ExpectedNotificationElementTitles.ToArray());
        }

        public void Deserialize(IXunitSerializationInfo info)
        {
            UserName = info.GetValue<string>(nameof(UserName)) ?? string.Empty;
            ModuleAuthorizationClaims = (info.GetValue<string[]>(nameof(ModuleAuthorizationClaims)) ?? []).Select(claimJson => JsonSerializer.Deserialize<ModuleAuthorizationClaim>(claimJson));
            ExpectedNotificationElementTitles = info.GetValue<string[]>(nameof(ExpectedNotificationElementTitles)) ?? [];
        }
    }
}
