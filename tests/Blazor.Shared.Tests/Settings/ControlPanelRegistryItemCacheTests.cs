using System.Security.Claims;
using System.Text.Json;
using Blazor.Shared.Authorization;
using Blazor.Shared.Authorization.Extensions;
using Blazor.Shared.Settings.Extensions;
using Blazor.Shared.Settings.Services;
using Blazor.Shared.Tests.Mocks;
using Blazor.Shared.Tests.Models;
using AwesomeAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Sdk.Authorization;
using Sdk.Authorization.Extensions;
using Sdk.Client.ControlPanels.Attributes;
using Sdk.Client.ControlPanels.Components;
using Sdk.Client.ControlPanels.Extensions;
using Sdk.Client.ControlPanels.Services;
using Sdk.Testing.Client;
using Xunit;
using Xunit.Abstractions;

namespace Blazor.Shared.Tests.Settings;

public sealed class ControlPanelRegistryItemCacheTests
{
    public static TheoryData<AuthorizationScenario> AuthorizationScenarios =>
    [
        new AuthorizationScenario
        {
            UserName = "Hans",
            ModuleAuthorizationClaims = [new(TestClientModuleA.ModuleId, AccessLevel.Full, TestClientModuleA.ModuleId)],
            ExpectedControlPanelTypes =
            [
                typeof(AdminControlPanel),
                typeof(UserControlPanel),
                typeof(AllowAnonymousControlPanel)
            ]
        },

        new AuthorizationScenario
        {
            UserName = "Fritz",
            ModuleAuthorizationClaims = [new(TestClientModuleA.ModuleId, AccessLevel.Partial, TestClientModuleA.ModuleId)],
            ExpectedControlPanelTypes =
            [
                typeof(UserControlPanel),
                typeof(AllowAnonymousControlPanel)
            ]
        },

        new AuthorizationScenario
        {
            UserName = "Anonymous",
            ModuleAuthorizationClaims = [],
            ExpectedControlPanelTypes =
            [
                typeof(AllowAnonymousControlPanel)
            ]
        }
    ];

    private static ServiceProvider SetupServiceProvider()
    {
        var services = new ServiceCollection()
            .AddClientServices(configurator => { })
            .AddScoped(_ => new AuthenticationStateProviderMock("Frodo", new ModuleAuthorizationClaim(TestClientModuleA.ModuleId, AccessLevel.Full, TestClientModuleA.ModuleId)))
            .AddScoped<AuthenticationStateProvider>(services => services.GetRequiredService<AuthenticationStateProviderMock>())
            .AddSettings();

        AddControlPanels(services);

        return services.BuildServiceProvider();
    }

    private static void AddControlPanels(IServiceCollection services)
    {
        services.AddControlPanel<TestClientModuleA, AdminControlPanel, ControlPanelState>()
            .WithAutoDiscovery<AdminControlPanelDescriptor>();

        services.AddControlPanel<TestClientModuleA, UserControlPanel, ControlPanelState>()
            .WithAutoDiscovery<UserControlPanelDescriptor>();

        services.AddControlPanel<TestClientModuleA, AllowAnonymousControlPanel, ControlPanelState>()
            .WithAutoDiscovery<AllowAnonymousControlPanelDescriptor>();
    }

    [Fact]
    public void Should_be_resolvable()
    {
        // Arrange
        using var serviceProvider = SetupServiceProvider();

        // Act
        var controlPanelRegistryItemCache = serviceProvider.GetRequiredService<IControlPanelRegistryItemCache>();

        // Assert
        controlPanelRegistryItemCache.Should().NotBeNull();
    }

    [Fact]
    public async Task Should_return_all_control_panel_registry_items_cached()
    {
        // Arrange
        await using var serviceProvider = SetupServiceProvider();

        var controlPanelRegistryItemCache = serviceProvider.GetRequiredService<IControlPanelRegistryItemCache>();

        var authenticationStateProvider = serviceProvider.GetRequiredService<AuthenticationStateProvider>();
        var user = await authenticationStateProvider.GetUser();

        var firstResult = await controlPanelRegistryItemCache.GetAll(user);
        var firstResultCopy = firstResult.ToList();

        // Act
        var secondResult = await controlPanelRegistryItemCache.GetAll(user);

        // Assert
        secondResult.Should().BeSameAs(firstResult);
        secondResult.Should().ContainInConsecutiveOrder(firstResultCopy);
    }

    [Fact]
    public async Task Should_return_updated_result_when_different_user_is_passed()
    {
        // Arrange
        await using var serviceProvider = SetupServiceProvider();
        var controlPanelRegistryItemCache = serviceProvider.GetRequiredService<IControlPanelRegistryItemCache>();

        var authenticationStateProvider = serviceProvider.GetRequiredService<AuthenticationStateProvider>();
        var currentUser = await authenticationStateProvider.GetUser();

        var firstResult = await controlPanelRegistryItemCache.GetAll(currentUser);

        // Act
        var newUser = new ClaimsPrincipal();

        var secondResult = await controlPanelRegistryItemCache.GetAll(newUser);

        // Assert
        secondResult.Should().NotBeSameAs(firstResult);
    }

    [Fact]
    public async Task Should_update_itself_on_authentication_state_change()
    {
        // Arrange
        await using var serviceProvider = SetupServiceProvider();
        var controlPanelRegistryItemCache = serviceProvider.GetRequiredService<IControlPanelRegistryItemCache>();
        var controlPanelRegistryItemCacheMonitor = controlPanelRegistryItemCache.Monitor();

        var authenticationStateProvider = serviceProvider.GetRequiredService<AuthenticationStateProviderMock>();
        var currentUser = await authenticationStateProvider.GetUser();

        var firstResult = await controlPanelRegistryItemCache.GetAll(currentUser);

        // Act
        var newUser = await authenticationStateProvider.ChangeUser("Waldo");

        var secondResult = await controlPanelRegistryItemCache.GetAll(newUser);

        // Assert
        controlPanelRegistryItemCacheMonitor.Should().Raise(nameof(controlPanelRegistryItemCache.Changed));

        secondResult.Should().NotBeSameAs(firstResult);
    }

    [Fact]
    public async Task Should_update_itself_on_control_panel_registry_changes()
    {
        // Arrange
        await using var serviceProvider = SetupServiceProvider();
        var controlPanelRegistryItemCache = serviceProvider.GetRequiredService<IControlPanelRegistryItemCache>();
        var controlPanelRegistryItemCacheMonitor = controlPanelRegistryItemCache.Monitor();

        var authenticationStateProvider = serviceProvider.GetRequiredService<AuthenticationStateProvider>();
        var currentUser = await authenticationStateProvider.GetUser();

        var firstResult = await controlPanelRegistryItemCache.GetAll(currentUser);

        var controlPanelRegistry = serviceProvider.GetRequiredService<IControlPanelRegistry<TestClientModuleA>>();

        // Act
        var newControlPanelRegistryItem = controlPanelRegistry.Add<AdminControlPanel, ControlPanelState>(
            new AdminControlPanelDescriptor(), new ControlPanelState(), new TestControlPanelCategoryDescriptor());

        var secondResult = await controlPanelRegistryItemCache.GetAll(currentUser);

        // Assert
        controlPanelRegistryItemCacheMonitor.Should().Raise(nameof(controlPanelRegistryItemCache.Changed));

        secondResult.Should().NotBeSameAs(firstResult);
        secondResult.Should().Contain(newControlPanelRegistryItem);
    }

    [Theory]
    [MemberData(nameof(AuthorizationScenarios))]
    public async Task Should_cache_control_panel_registry_items_based_on_authorization_configuration(AuthorizationScenario authorizationScenario)
    {
        // Arrange
        var services = new ServiceCollection()
            .AddClientServices(configurator => { })
            .AddScoped(_ => new AuthenticationStateProviderMock(authorizationScenario.UserName, authorizationScenario.ModuleAuthorizationClaims))
            .AddScoped<AuthenticationStateProvider>(services => services.GetRequiredService<AuthenticationStateProviderMock>())
            .AddSettings()
            .AddLogging()
            .AddSdkAuthorization()
            .AddSingleton<IAuthorizationHandler, ModuleAccessLevelHandler>();

        AddControlPanels(services);

        using var serviceProvider = services.BuildServiceProvider();

        var controlPanelRegistryItemCache = serviceProvider.GetRequiredService<IControlPanelRegistryItemCache>();
        var authenticationStateProvider = serviceProvider.GetRequiredService<AuthenticationStateProvider>();

        var user = await authenticationStateProvider.GetUser();

        // Act
        var controlPanelRegistryItems = await controlPanelRegistryItemCache.GetAll(user);

        // Assert
        var controlPanelTypes = controlPanelRegistryItems.Select(i => i.ComponentType);

        controlPanelTypes.Should().BeEquivalentTo(authorizationScenario.ExpectedControlPanelTypes);
    }

    private sealed class TestControlPanelCategoryDescriptor : IControlPanelCategoryDescriptor
    {
        public string Title => "Category";
        public string? IconCssClass => "icon";
        public Uri? IconUrl => null;
        public int? Position => null;
    }

    [ControlPanelCategory<TestControlPanelCategoryDescriptor>]
    [ModuleAuthorize(TestClientModuleA.ModuleId, AccessLevel.Full)]
    private sealed class AdminControlPanel : ControlPanelBase<ControlPanelState>
    {
    }

    private sealed class AdminControlPanelDescriptor : IControlPanelDescriptor<AdminControlPanel>
    {
        public string Title => "Foo";
        public string IconPath => "icon.svg";
    }

    [ControlPanelCategory<TestControlPanelCategoryDescriptor>]
    [ModuleAuthorize(TestClientModuleA.ModuleId, AccessLevel.Partial)]
    private sealed class UserControlPanel : ControlPanelBase<ControlPanelState>
    {
    }

    private sealed class UserControlPanelDescriptor : IControlPanelDescriptor<UserControlPanel>
    {
        public string Title => "Bar";
        public string IconPath => "icon.svg";
    }

    [ControlPanelCategory<TestControlPanelCategoryDescriptor>]
    private sealed class AllowAnonymousControlPanel : ControlPanelBase<ControlPanelState>
    {
    }

    private sealed class AllowAnonymousControlPanelDescriptor : IControlPanelDescriptor<AllowAnonymousControlPanel>
    {
        public string Title => "Anonymous";
        public string IconPath => "icon.svg";
    }

    public sealed class AuthorizationScenario : IXunitSerializable
    {
        public required string UserName { get; set; }
        public required IEnumerable<ModuleAuthorizationClaim> ModuleAuthorizationClaims { get; set; }
        public required IEnumerable<Type> ExpectedControlPanelTypes { get; set; }

        public void Serialize(IXunitSerializationInfo info)
        {
            info.AddValue(nameof(UserName), UserName);
            info.AddValue(nameof(ModuleAuthorizationClaims), ModuleAuthorizationClaims.Select(claim => JsonSerializer.Serialize(claim)).ToArray());
            info.AddValue(nameof(ExpectedControlPanelTypes), ExpectedControlPanelTypes.Select(type => type.FullName).ToArray());
        }

        public void Deserialize(IXunitSerializationInfo info)
        {
            UserName = info.GetValue<string>(nameof(UserName));
            ModuleAuthorizationClaims = info.GetValue<string[]>(nameof(ModuleAuthorizationClaims)).Select(claimJson => JsonSerializer.Deserialize<ModuleAuthorizationClaim>(claimJson));
            ExpectedControlPanelTypes = info.GetValue<string[]>(nameof(ExpectedControlPanelTypes)).Select(typeNames => Type.GetType(typeNames)!);
        }
    }
}
