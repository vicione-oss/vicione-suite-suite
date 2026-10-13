using Blazor.Server.Backend.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;
using Microsoft.JSInterop.Infrastructure;
using Sdk.Client.NotificationArea.Services;
using Sdk.Client.Services;

namespace Blazor.Server.Tests.Services;

public sealed class NavigationServiceTests
{
    private const string BaseUri = "http://localhost:2112/";
    private const string TestUri = "http://localhost:2112/test";

    private readonly ILayoutService _layoutService = Substitute.For<ILayoutService>();
    private readonly IActiveNotificationElementPolicy _activeNotificationElementPolicy = Substitute.For<IActiveNotificationElementPolicy>();
    private readonly IJSRuntime _jsRuntime = Substitute.For<IJSRuntime>();
    private readonly ILogger<NavigationService> _logger = Substitute.For<ILogger<NavigationService>>();

    public NavigationServiceTests() => _logger.IsEnabled(Arg.Any<LogLevel>()).Returns(true);

    [Fact]
    public void Should_not_set_location_values_when_base_uri_and_uri_are_different()
    {
        // Arrange
        var navigationManager = new MockNavigationManager(BaseUri, TestUri);
        using var navigationService = new NavigationService(navigationManager, _layoutService,
            _activeNotificationElementPolicy, _jsRuntime, _logger);

        // Act
        // Calling the method "NavigationManager_LocationChanged" of the NavigationService is carried out
        // in the method "NotifyLocationChanged" of the base class of the NavigationManager.
        // (_locationChanged?.Invoke .... )
        navigationManager.NotifyLocationChanged(true);

        // Assert
        // Parameter were not set.
        _layoutService.Received(0).TitleBarAppName = string.Empty;
        _layoutService.Received(0).IsLoadingOverlayVisible = false;
    }

    [Fact]
    public void Should_set_location_values_when_base_uri_and_uri_are_equal()
    {
        // Arrange
        var navigationManager = new MockNavigationManager(BaseUri, BaseUri);
        using var navigationService = new NavigationService(navigationManager, _layoutService,
            _activeNotificationElementPolicy, _jsRuntime, _logger);

        // Act
        // Calling the method "NavigationManager_LocationChanged" of the NavigationService is carried out
        // in the method "NotifyLocationChanged" of the base class of the NavigationManager.
        // (_locationChanged?.Invoke .... )
        navigationManager.NotifyLocationChanged(true);

        // Assert
        // Parameter were set.
        _layoutService.Received(1).TitleBarAppName = string.Empty;
        _layoutService.Received(1).IsLoadingOverlayVisible = false;
    }

    [Fact]
    public async Task Should_execute_logout_steps()
    {
        // Arrange
        var navigationManager = new MockNavigationManager(BaseUri, BaseUri);
        using var navigationService = new NavigationService(navigationManager, _layoutService, _activeNotificationElementPolicy, _jsRuntime, _logger);

        // Act
        await navigationService.Logout();

        // Assert
        _activeNotificationElementPolicy.Received(1).NoneActive();

        await _jsRuntime.Received(1).InvokeAsync<IJSVoidResult>(
            "ViciOne.Interop.signOut",
            Arg.Is<object?[]?>(args => args != null && (string)args[0]! == $"{BaseUri}account/logout"));
    }

    [Fact]
    public async Task Should_redirect_to_login_page_when_redirect_is_called()
    {
        // Arrange
        var navigationManager = new MockNavigationManager(BaseUri, TestUri);
        using var navigationService = new NavigationService(navigationManager, _layoutService, _activeNotificationElementPolicy, _jsRuntime, _logger);

        // Act
        navigationService.RedirectToSignIn();

        // Assert
        await _jsRuntime.Received(1).InvokeAsync<IJSVoidResult>(
            "ViciOne.Interop.redirectToSignIn",
            Arg.Is<object?[]?>(args => args != null && (string)args[0]! == $"{BaseUri}account/login"));
        navigationManager.RedirectUri.Should().BeEmpty();
    }

    [Fact]
    public async Task Should_log_an_error_instead_of_throwing_when_the_sign_out_script_fails()
    {
        // Arrange
        var jsRuntime = new ThrowingJsRuntime(new JSException("ViciOne.Interop is not loaded"));
        using var navigationService = new NavigationService(new MockNavigationManager(BaseUri, BaseUri), _layoutService,
            _activeNotificationElementPolicy, jsRuntime, _logger);

        // Act
        var logout = () => navigationService.Logout();

        // Assert
        await logout.Should().NotThrowAsync();
        LoggedCalls(LogLevel.Error).Should().Be(1);
    }

    [Fact]
    public void Should_log_an_error_when_the_sign_in_redirect_script_fails()
    {
        // Arrange
        var jsRuntime = new ThrowingJsRuntime(new JSException("ViciOne.Interop is not loaded"));
        using var navigationService = new NavigationService(new MockNavigationManager(BaseUri, TestUri), _layoutService,
            _activeNotificationElementPolicy, jsRuntime, _logger);

        // Act
        navigationService.RedirectToSignIn();

        // Assert
        LoggedCalls(LogLevel.Error).Should().Be(1);
    }

    [Fact]
    public async Task Should_not_log_an_error_when_the_ending_circuit_cancels_the_sign_out_call()
    {
        // Arrange
        var jsRuntime = new ThrowingJsRuntime(new JSDisconnectedException("Circuit ended"));
        using var navigationService = new NavigationService(new MockNavigationManager(BaseUri, BaseUri), _layoutService,
            _activeNotificationElementPolicy, jsRuntime, _logger);

        // Act
        await navigationService.Logout();

        // Assert
        LoggedCalls(LogLevel.Error).Should().Be(0);
        LoggedCalls(LogLevel.Debug).Should().Be(1);
    }

    private int LoggedCalls(LogLevel level)
        => _logger.ReceivedCalls()
            .Count(call => call.GetMethodInfo().Name == nameof(ILogger.Log) && call.GetArguments()[0] is LogLevel logLevel && logLevel == level);

    private sealed class ThrowingJsRuntime(Exception exception) : IJSRuntime
    {
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args)
            => ValueTask.FromException<TValue>(exception);

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args)
            => ValueTask.FromException<TValue>(exception);
    }

    private class MockNavigationManager : NavigationManager
    {
        public string RedirectUri { get; private set; } = "";

        public MockNavigationManager(string baseUri, string uri) => Initialize(baseUri, uri);

        protected override void NavigateToCore(string uri, bool forceLoad) => RedirectUri = uri;

        public new void NotifyLocationChanged(bool v) => base.NotifyLocationChanged(v);
    }
}
