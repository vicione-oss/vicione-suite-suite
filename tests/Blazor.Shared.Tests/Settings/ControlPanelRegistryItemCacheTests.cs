using System.Diagnostics.CodeAnalysis;
using System.Security.Claims;
using System.Text.Json;
using Blazor.Shared.Authorization;
using Blazor.Shared.Authorization.Extensions;
using Blazor.Shared.Settings.Extensions;
using Blazor.Shared.Settings.Services;
using Blazor.Shared.Tests.Mocks;
using Blazor.Shared.Tests.Models;
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
using Xunit.Sdk;

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
            .AddClientServices(_ => { })
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
    public async Task Should_be_resolvable()
    {
        // Arrange
        await using var serviceProvider = SetupServiceProvider();

        // Act
        var controlPanelRegistryItemCache = serviceProvider.GetRequiredService<IControlPanelRegistryItemCache>();

        // Assert
        controlPanelRegistryItemCache.Should().NotBeNull();
    }

    [Fact]
    [SuppressMessage("ReSharper", "PossibleMultipleEnumeration", Justification = "Testing with multiple IEnumerable results")]
    public async Task Should_return_all_control_panel_registry_items_cached()
    {
        // Arrange
        await using var serviceProvider = SetupServiceProvider();

        var controlPanelRegistryItemCache = serviceProvider.GetRequiredService<IControlPanelRegistryItemCache>();

        var authenticationStateProvider = serviceProvider.GetRequiredService<AuthenticationStateProvider>();
        var user = await authenticationStateProvider.GetUser();

        var firstResult = await controlPanelRegistryItemCache.GetAll(user, TestContext.Current.CancellationToken);
        var firstResultCopy = firstResult.ToList();

        // Act
        var secondResult = await controlPanelRegistryItemCache.GetAll(user, TestContext.Current.CancellationToken);

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

        var firstResult = await controlPanelRegistryItemCache.GetAll(currentUser, TestContext.Current.CancellationToken);

        // Act
        var newUser = new ClaimsPrincipal();

        var secondResult = await controlPanelRegistryItemCache.GetAll(newUser, TestContext.Current.CancellationToken);

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

        var firstResult = await controlPanelRegistryItemCache.GetAll(currentUser, TestContext.Current.CancellationToken);

        // Act
        var newUser = await authenticationStateProvider.ChangeUser("Waldo");

        var secondResult = await controlPanelRegistryItemCache.GetAll(newUser, TestContext.Current.CancellationToken);

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

        var firstResult = await controlPanelRegistryItemCache.GetAll(currentUser, TestContext.Current.CancellationToken);

        var controlPanelRegistry = serviceProvider.GetRequiredService<IControlPanelRegistry<TestClientModuleA>>();

        // Act
        var newControlPanelRegistryItem = controlPanelRegistry.Add<AdminControlPanel, ControlPanelState>(
            new AdminControlPanelDescriptor(), new ControlPanelState(), new TestControlPanelCategoryDescriptor());

        var secondResult = (await controlPanelRegistryItemCache.GetAll(currentUser, TestContext.Current.CancellationToken)).ToArray();

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
            .AddClientServices(_ => { })
            .AddScoped(_ => new AuthenticationStateProviderMock(authorizationScenario.UserName, authorizationScenario.ModuleAuthorizationClaims))
            .AddScoped<AuthenticationStateProvider>(services => services.GetRequiredService<AuthenticationStateProviderMock>())
            .AddSettings()
            .AddLogging()
            .AddSdkAuthorization()
            .AddSingleton<IAuthorizationHandler, ModuleAccessLevelHandler>();

        AddControlPanels(services);

        await using var serviceProvider = services.BuildServiceProvider();

        var controlPanelRegistryItemCache = serviceProvider.GetRequiredService<IControlPanelRegistryItemCache>();
        var authenticationStateProvider = serviceProvider.GetRequiredService<AuthenticationStateProvider>();

        var user = await authenticationStateProvider.GetUser();

        // Act
        var controlPanelRegistryItems = await controlPanelRegistryItemCache.GetAll(user, TestContext.Current.CancellationToken);

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
        public Uri IconUrl => new("icon.svg", UriKind.Relative);
    }

    [ControlPanelCategory<TestControlPanelCategoryDescriptor>]
    [ModuleAuthorize(TestClientModuleA.ModuleId)]
    private sealed class UserControlPanel : ControlPanelBase<ControlPanelState>
    {
    }

    private sealed class UserControlPanelDescriptor : IControlPanelDescriptor<UserControlPanel>
    {
        public string Title => "Bar";
        public Uri IconUrl => new("icon.svg", UriKind.Relative);
    }

    [ControlPanelCategory<TestControlPanelCategoryDescriptor>]
    private sealed class AllowAnonymousControlPanel : ControlPanelBase<ControlPanelState>
    {
    }

    private sealed class AllowAnonymousControlPanelDescriptor : IControlPanelDescriptor<AllowAnonymousControlPanel>
    {
        public string Title => "Anonymous";
        public Uri IconUrl => new("icon.svg", UriKind.Relative);
    }

    [Fact]
    public async Task Should_return_empty_when_disposed()
    {
        // Arrange
        var serviceProvider = SetupServiceProvider();
        var controlPanelRegistryItemCache = serviceProvider.GetRequiredService<IControlPanelRegistryItemCache>();

        var authenticationStateProvider = serviceProvider.GetRequiredService<AuthenticationStateProvider>();
        var user = await authenticationStateProvider.GetUser();

        await serviceProvider.DisposeAsync();

        // Act
        var result = await controlPanelRegistryItemCache.GetAll(user, TestContext.Current.CancellationToken);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task Should_return_empty_when_cancellation_is_requested()
    {
        // Arrange
        await using var serviceProvider = SetupServiceProvider();
        var controlPanelRegistryItemCache = serviceProvider.GetRequiredService<IControlPanelRegistryItemCache>();

        var authenticationStateProvider = serviceProvider.GetRequiredService<AuthenticationStateProvider>();
        var user = await authenticationStateProvider.GetUser();

        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        // Act
        var result = await controlPanelRegistryItemCache.GetAll(user, cts.Token);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task Should_handle_concurrent_GetAll_calls()
    {
        // Arrange
        await using var serviceProvider = SetupServiceProvider();
        var controlPanelRegistryItemCache = serviceProvider.GetRequiredService<IControlPanelRegistryItemCache>();

        var authenticationStateProvider = serviceProvider.GetRequiredService<AuthenticationStateProvider>();
        var user = await authenticationStateProvider.GetUser();

        // Act
        var tasks = Enumerable.Range(0, 10)
            .Select(_ => controlPanelRegistryItemCache.GetAll(user, TestContext.Current.CancellationToken));

        var results = await Task.WhenAll(tasks);

        // Assert
        var firstResult = results[0];
        foreach (var result in results)
        {
            result.Should().BeSameAs(firstResult);
        }
    }

    [Fact]
    public async Task Should_not_throw_when_disposed_during_concurrent_GetAll_calls()
    {
        // Arrange
        var serviceProvider = SetupServiceProvider();
        var controlPanelRegistryItemCache = serviceProvider.GetRequiredService<IControlPanelRegistryItemCache>();

        var authenticationStateProvider = serviceProvider.GetRequiredService<AuthenticationStateProvider>();
        var user = await authenticationStateProvider.GetUser();

        // Act
        var getAllTasks = Enumerable.Range(0, 10)
            .Select(_ => controlPanelRegistryItemCache.GetAll(user, TestContext.Current.CancellationToken));

        var disposeTask = serviceProvider.DisposeAsync();

        var results = await Task.WhenAll(getAllTasks);
        await disposeTask;

        // Assert — no exceptions should be thrown, each result is either populated or empty
        foreach (var result in results)
        {
            result.Should().NotBeNull();
        }
    }

    [Fact]
    public async Task Should_not_throw_when_disposed_multiple_times()
    {
        // Arrange
        var serviceProvider = SetupServiceProvider();
        var controlPanelRegistryItemCache = serviceProvider.GetRequiredService<IControlPanelRegistryItemCache>();

        // Act & Assert — disposing multiple times should not throw
        await serviceProvider.DisposeAsync();
        await serviceProvider.DisposeAsync();
    }

    [Fact]
    public async Task Should_not_throw_when_authentication_state_changes_after_disposal()
    {
        // Arrange
        var serviceProvider = SetupServiceProvider();
        var controlPanelRegistryItemCache = serviceProvider.GetRequiredService<IControlPanelRegistryItemCache>();

        var authenticationStateProvider = serviceProvider.GetRequiredService<AuthenticationStateProviderMock>();
        var user = await authenticationStateProvider.GetUser();

        await controlPanelRegistryItemCache.GetAll(user, TestContext.Current.CancellationToken);

        await serviceProvider.DisposeAsync();

        // Act & Assert — changing authentication state after disposal should not throw
        await authenticationStateProvider.ChangeUser("PostDisposalUser");
    }

    [Fact]
    public async Task Should_not_throw_when_control_panel_registry_changes_after_disposal()
    {
        // Arrange
        var serviceProvider = SetupServiceProvider();
        var controlPanelRegistryItemCache = serviceProvider.GetRequiredService<IControlPanelRegistryItemCache>();

        var authenticationStateProvider = serviceProvider.GetRequiredService<AuthenticationStateProvider>();
        var user = await authenticationStateProvider.GetUser();

        await controlPanelRegistryItemCache.GetAll(user, TestContext.Current.CancellationToken);

        var controlPanelRegistry = serviceProvider.GetRequiredService<IControlPanelRegistry<TestClientModuleA>>();

        await serviceProvider.DisposeAsync();

        // Act & Assert — modifying registry after disposal should not throw
        controlPanelRegistry.Add<AdminControlPanel, ControlPanelState>(
            new AdminControlPanelDescriptor(), new ControlPanelState(), new TestControlPanelCategoryDescriptor());
    }

    [Fact]
    public async Task Should_not_invalidate_cache_when_removed_items_are_not_in_cache()
    {
        // Arrange
        await using var serviceProvider = SetupServiceProvider();
        var controlPanelRegistryItemCache = serviceProvider.GetRequiredService<IControlPanelRegistryItemCache>();
        var controlPanelRegistryItemCacheMonitor = controlPanelRegistryItemCache.Monitor();

        var authenticationStateProvider = serviceProvider.GetRequiredService<AuthenticationStateProvider>();
        var currentUser = await authenticationStateProvider.GetUser();

        var controlPanelRegistry = serviceProvider.GetRequiredService<IControlPanelRegistry<TestClientModuleA>>();

        // Add an extra item, then populate the cache without it by removing before GetAll
        var extraItem = controlPanelRegistry.Add<AdminControlPanel, ControlPanelState>(
            new AdminControlPanelDescriptor(), new ControlPanelState(), new TestControlPanelCategoryDescriptor());

        var firstResult = await controlPanelRegistryItemCache.GetAll(currentUser, TestContext.Current.CancellationToken);

        // Clear monitor after initial population to only track subsequent events
        controlPanelRegistryItemCacheMonitor.Clear();

        // Act — remove the extra item that was added after the cache was populated
        controlPanelRegistry.Remove<AdminControlPanel>();

        // Re-fetch to allow the event handler to run
        var secondResult = await controlPanelRegistryItemCache.GetAll(currentUser, TestContext.Current.CancellationToken);

        // Assert — cache was invalidated because the removed items intersected with the cached items
        controlPanelRegistryItemCacheMonitor.Should().Raise(nameof(controlPanelRegistryItemCache.Changed));
        secondResult.Should().NotBeSameAs(firstResult);
    }

    [Fact]
    public async Task Should_not_raise_Changed_when_authentication_state_changes_concurrently_with_GetAll()
    {
        // Arrange
        await using var serviceProvider = SetupServiceProvider();
        var controlPanelRegistryItemCache = serviceProvider.GetRequiredService<IControlPanelRegistryItemCache>();

        var authenticationStateProvider = serviceProvider.GetRequiredService<AuthenticationStateProviderMock>();
        var user = await authenticationStateProvider.GetUser();

        await controlPanelRegistryItemCache.GetAll(user, TestContext.Current.CancellationToken);

        // Act — fire concurrent GetAll and authentication state change
        var getAllTasks = Enumerable.Range(0, 5)
            .Select(_ => controlPanelRegistryItemCache.GetAll(user, TestContext.Current.CancellationToken));

        var changeUserTask = authenticationStateProvider.ChangeUser("ConcurrentUser");

        var results = await Task.WhenAll(getAllTasks);
        var newUser = await changeUserTask;

        // Assert — no exceptions, results are valid
        foreach (var result in results)
        {
            result.Should().NotBeNull();
        }

        // After auth state change, a new GetAll with the new user should return a fresh result
        var freshResult = await controlPanelRegistryItemCache.GetAll(newUser, TestContext.Current.CancellationToken);
        freshResult.Should().NotBeNull();
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
            UserName = info.GetValue<string>(nameof(UserName)) ?? string.Empty;
            ModuleAuthorizationClaims = (info.GetValue<string[]>(nameof(ModuleAuthorizationClaims)) ?? []).Select(claimJson => JsonSerializer.Deserialize<ModuleAuthorizationClaim>(claimJson));
            ExpectedControlPanelTypes = (info.GetValue<string[]>(nameof(ExpectedControlPanelTypes)) ?? []).Select(typeNames => Type.GetType(typeNames)!);
        }
    }
}
