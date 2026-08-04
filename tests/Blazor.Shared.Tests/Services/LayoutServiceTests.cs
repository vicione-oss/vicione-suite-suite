using System.ComponentModel;
using Blazor.Shared.Services;
using Sdk.Client.Infrastructure;
using Sdk.Instance;

namespace Blazor.Shared.Tests.Services;

public sealed class LayoutServiceTests : IDisposable
{
    private const string TitleBarFirstValue = "FirstValue";
    private const string TitleBarSecondValue = "SecondValue";

    private readonly IInstanceInformationProvider _instanceInformationProvider = Substitute.For<IInstanceInformationProvider>();
    private readonly IUiMediator _uiMediator = Substitute.For<IUiMediator>();
    private readonly LayoutService _service;
    private string? _propertyName;

    public LayoutServiceTests() => _service = new LayoutService(_instanceInformationProvider, _uiMediator);

    [Fact]
    public void Should_change_title_bar_app_name_when_set_to_new_value()
    {
        // Arrange
        _service.PropertyChanged += PropertyHasChanged;

        _service.TitleBarAppName = TitleBarFirstValue;
        _propertyName = string.Empty;

        // Act
        _service.TitleBarAppName = TitleBarSecondValue;

        // Assert
        _service.TitleBarAppName.Should().Be(TitleBarSecondValue);
        _propertyName.Should().Be(nameof(LayoutService.TitleBarAppName)); // check that property has changed
    }

    [Fact]
    public void Should_change_title_bar_text_when_set_to_new_value()
    {
        // Arrange
        _service.PropertyChanged += PropertyHasChanged;

        _service.TitleBarAppName = TitleBarFirstValue;
        _propertyName = string.Empty;

        // Act
        _service.TitleBarText = TitleBarSecondValue;

        // Assert
        _service.TitleBarText.Should().Be(TitleBarSecondValue);
        _propertyName.Should().Be(nameof(LayoutService.TitleBarText)); // check that property has changed
    }

    [Fact]
    public void Should_change_is_sidebar_open_when_set_to_new_value()
    {
        // Arrange
        _service.PropertyChanged += PropertyHasChanged;

        _service.IsSidebarOpen = false;
        _propertyName = string.Empty;

        // Act
        _service.IsSidebarOpen = true;

        // Assert
        _service.IsSidebarOpen.Should().BeTrue();
        _propertyName.Should().Be(nameof(LayoutService.IsSidebarOpen)); // check that property has changed
    }

    [Fact]
    public void Should_change_is_loading_overlay_visible_when_set_to_new_value()
    {
        // Arrange
        _service.PropertyChanged += PropertyHasChanged;

        _service.IsLoadingOverlayVisible = false;
        _propertyName = string.Empty;

        // Act
        _service.IsLoadingOverlayVisible = true;

        // Assert
        _service.IsLoadingOverlayVisible.Should().BeTrue();
        _propertyName.Should().Be(nameof(LayoutService.IsLoadingOverlayVisible)); // check that property has changed
    }

    [Fact]
    public void Should_not_change_title_bar_app_name_when_set_to_same_value()
    {
        // Arrange
        _service.PropertyChanged += PropertyHasChanged;

        _service.TitleBarAppName = TitleBarFirstValue;
        _propertyName = string.Empty;

        // Act
        _service.TitleBarAppName = TitleBarFirstValue;

        // Assert
        _service.TitleBarAppName.Should().Be(TitleBarFirstValue);
        _propertyName.Should().Be(string.Empty); // check that property has not changed
    }

    [Fact]
    public void Should_not_change_title_bar_text_when_set_to_same_value()
    {
        // Arrange
        _service.PropertyChanged += PropertyHasChanged;

        _service.TitleBarText = TitleBarFirstValue;
        _propertyName = string.Empty;

        // Act
        _service.TitleBarText = TitleBarFirstValue;

        // Assert
        _service.TitleBarText.Should().Be(TitleBarFirstValue);
        _propertyName.Should().Be(string.Empty); // check that property has not changed
    }

    [Fact]
    public void Should_not_change_is_sidebar_open_when_set_to_same_value()
    {
        // Arrange
        _service.PropertyChanged += PropertyHasChanged;

        _service.IsSidebarOpen = false;
        _propertyName = string.Empty;

        // Act
        _service.IsSidebarOpen = false;

        // Assert
        _service.IsSidebarOpen.Should().BeFalse();
        _propertyName.Should().Be(string.Empty); // check that property has not changed
    }

    [Fact]
    public void Should_not_change_is_loading_overlay_visible_when_set_to_same_value()
    {
        // Arrange
        _service.PropertyChanged += PropertyHasChanged;

        _service.IsLoadingOverlayVisible = false;
        _propertyName = string.Empty;

        // Act
        _service.IsLoadingOverlayVisible = false;

        // Assert
        _service.IsLoadingOverlayVisible.Should().BeFalse();
        _propertyName.Should().Be(string.Empty); // check that property has not changed
    }

    private void PropertyHasChanged(object? sender, PropertyChangedEventArgs e)
        => _propertyName = e.PropertyName;

    public void Dispose() => _service.Dispose();
}
