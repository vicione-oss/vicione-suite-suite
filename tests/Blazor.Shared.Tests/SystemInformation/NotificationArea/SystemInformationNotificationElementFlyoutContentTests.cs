using Blazor.Shared.Services;
using Blazor.Shared.SystemInformation.Extensions;
using Blazor.Shared.SystemInformation.NotificationArea;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Sdk.Client.Infrastructure;
using Sdk.Instance;

namespace Blazor.Shared.Tests.SystemInformation.NotificationArea;

public sealed class SystemInformationNotificationElementFlyoutContentTests
{
    [Fact]
    public void Should_render_component()
    {
        // Arrange
        using var ctx = new BunitContext();

        ctx.Services
            .AddSystemInformation()
            .AddSingleton(Substitute.For<IUiMediator>())
            .AddSingleton(Substitute.For<IInstanceInformationProvider>())
            .AddSingleton(Substitute.For<ISuiteControlService>())
            .AddSingleton(Substitute.For<INavigationService>());

        // Act
        var component = ctx.Render<SystemInformationNotificationElementFlyoutContent>();

        // Assert
        component.Should().NotBeNull();
    }
}
