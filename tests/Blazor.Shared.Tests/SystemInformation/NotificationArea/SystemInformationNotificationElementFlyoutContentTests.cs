using Blazor.Shared.Services;
using Blazor.Shared.SystemInformation.Extensions;
using Blazor.Shared.SystemInformation.NotificationArea;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Xunit;

namespace Blazor.Shared.Tests.SystemInformation.NotificationArea;

public sealed class SystemInformationNotificationElementFlyoutContentTests
{
    [Fact]
    public void Should_render_component()
    {
        // Arrange
        using var ctx = new TestContext();

        ctx.Services
            .AddSystemInformation()
            .AddSingleton<ISuiteControlService>(Substitute.For<ISuiteControlService>());

        // Act
        var component = ctx.RenderComponent<SystemInformationNotificationElementFlyoutContent>();

        // Assert
        Assert.NotNull(component);
    }
}
