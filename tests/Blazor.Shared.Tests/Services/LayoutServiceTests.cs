using System.ComponentModel;
using Blazor.Shared.Services;
using NSubstitute;
using Sdk.Client.Infrastructure;
using Sdk.Instance;
using Xunit;

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
    public void Check_property_changed_title_bar_app_name_if_set_new_value()
    {
        // Arrange
        _service.PropertyChanged += PropertyHasChanged;

        _service.TitleBarAppName = TitleBarFirstValue;
        _propertyName = string.Empty;

        // Act
        _service.TitleBarAppName = TitleBarSecondValue;

        // Assert
        Assert.Equal(TitleBarSecondValue, _service.TitleBarAppName);
        Assert.Equal(nameof(LayoutService.TitleBarAppName), _propertyName); // check that property has changed
    }

    [Fact]
    public void Check_property_changed_title_bar_text_if_set_new_value()
    {
        // Arrange
        _service.PropertyChanged += PropertyHasChanged;

        _service.TitleBarAppName = TitleBarFirstValue;
        _propertyName = string.Empty;

        // Act
        _service.TitleBarText = TitleBarSecondValue;

        // Assert
        Assert.Equal(TitleBarSecondValue, _service.TitleBarText);
        Assert.Equal(nameof(LayoutService.TitleBarText), _propertyName); // check that property has changed
    }

    [Fact]
    public void Check_property_changed_is_sidebar_open_if_set_new_value()
    {
        // Arrange
        _service.PropertyChanged += PropertyHasChanged;

        _service.IsSidebarOpen = false;
        _propertyName = string.Empty;

        // Act
        _service.IsSidebarOpen = true;

        // Assert
        Assert.True(_service.IsSidebarOpen);
        Assert.Equal(nameof(LayoutService.IsSidebarOpen), _propertyName); // check that property has changed
    }

    [Fact]
    public void Check_property_changed_is_loading_overlay_visible_if_set_new_value()
    {
        // Arrange
        _service.PropertyChanged += PropertyHasChanged;

        _service.IsLoadingOverlayVisible = false;
        _propertyName = string.Empty;

        // Act
        _service.IsLoadingOverlayVisible = true;

        // Assert
        Assert.True(_service.IsLoadingOverlayVisible);
        Assert.Equal(nameof(LayoutService.IsLoadingOverlayVisible), _propertyName); // check that property has changed
    }

    [Fact]
    public void Check_property_not_changed_title_bar_app_name_if_set_same_value()
    {
        // Arrange
        _service.PropertyChanged += PropertyHasChanged;

        _service.TitleBarAppName = TitleBarFirstValue;
        _propertyName = string.Empty;

        // Act
        _service.TitleBarAppName = TitleBarFirstValue;

        // Assert
        Assert.Equal(TitleBarFirstValue, _service.TitleBarAppName);
        Assert.Equal(string.Empty, _propertyName); // check that property has not changed
    }

    [Fact]
    public void Check_property_not_changed_title_bar_text_if_set_same_value()
    {
        // Arrange
        _service.PropertyChanged += PropertyHasChanged;

        _service.TitleBarText = TitleBarFirstValue;
        _propertyName = string.Empty;

        // Act
        _service.TitleBarText = TitleBarFirstValue;

        // Assert
        Assert.Equal(TitleBarFirstValue, _service.TitleBarText);
        Assert.Equal(string.Empty, _propertyName); // check that property has not changed
    }

    [Fact]
    public void Check_property_not_changed_is_sidebar_open_if_set_same_value()
    {
        // Arrange
        _service.PropertyChanged += PropertyHasChanged;

        _service.IsSidebarOpen = false;
        _propertyName = string.Empty;

        // Act
        _service.IsSidebarOpen = false;

        // Assert
        Assert.False(_service.IsSidebarOpen);
        Assert.Equal(string.Empty, _propertyName); // check that property has not changed
    }

    [Fact]
    public void Check_property_not_changed_is_loading_overlay_visible_if_set_same_value()
    {
        // Arrange
        _service.PropertyChanged += PropertyHasChanged;

        _service.IsLoadingOverlayVisible = false;
        _propertyName = string.Empty;

        // Act
        _service.IsLoadingOverlayVisible = false;

        // Assert
        Assert.False(_service.IsLoadingOverlayVisible);
        Assert.Equal(string.Empty, _propertyName); // check that property has not changed
    }

    private void PropertyHasChanged(object? sender, PropertyChangedEventArgs e)
        => _propertyName = e.PropertyName;

    public void Dispose() => _service.Dispose();
}
