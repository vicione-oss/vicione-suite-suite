using Blazor.Server.Backend.Services;
using AwesomeAssertions;
using Microsoft.AspNetCore.Components;
using NSubstitute;
using Sdk.Client.NotificationArea.Services;
using Sdk.Client.Services;
using Xunit;

namespace Blazor.Server.Tests.Services;

public sealed class NavigationServiceTests
{
    private const string BaseUri = "http://localhost:2112/";
    private const string TestUri = "http://localhost:2112/test";

    private readonly ILayoutService _layoutService = Substitute.For<ILayoutService>();
    private readonly IActiveNotificationElementPolicy _activeNotificationElementPolicy = Substitute.For<IActiveNotificationElementPolicy>();
    private readonly IJsInterop _jsInterOp = Substitute.For<IJsInterop>();

    [Fact]
    public void Location_values_not_set_if_baseuri_and_uri_are_different()
    {
        // Arrange
        var navigationManager = new MockNavigationManager(BaseUri, TestUri);
        using var navigationService = new NavigationService(navigationManager, _layoutService,
            _activeNotificationElementPolicy, _jsInterOp);

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
    public void Location_values_set_if_baseuri_and_uri_are_equal()
    {
        // Arrange
        var navigationManager = new MockNavigationManager(BaseUri, BaseUri);
        using var navigationService = new NavigationService(navigationManager, _layoutService,
            _activeNotificationElementPolicy, _jsInterOp);

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
    public void Check_logout_steps()
    {
        // Arrange
        var navigationManager = new MockNavigationManager(BaseUri, BaseUri);
        using var navigationService = new NavigationService(navigationManager, _layoutService, _activeNotificationElementPolicy, _jsInterOp);

        // Act
        navigationService.Logout();

        // Assert
        _activeNotificationElementPolicy.Received(1).NoneActive();

        _jsInterOp.Received(1).SubmitForm(Arg.Is((string uri) => uri.Contains(BaseUri)), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public void Jump_to_login_page_if_call_redirect()
    {
        // Arrange
        var navigationManager = new MockNavigationManager(BaseUri, TestUri);
        using var navigationService = new NavigationService(navigationManager, _layoutService, _activeNotificationElementPolicy, _jsInterOp);

        // Act
        navigationService.RedirectToSignIn();

        // Assert
        navigationManager.RedirectUri.Should().Contain(BaseUri);
    }

    private class MockNavigationManager : NavigationManager
    {
        public string RedirectUri { get; private set; } = "";

        public MockNavigationManager(string baseUri, string uri) => Initialize(baseUri, uri);

        protected override void NavigateToCore(string uri, bool forceLoad) => RedirectUri = uri;

        public new void NotifyLocationChanged(bool v) => base.NotifyLocationChanged(v);
    }
}
