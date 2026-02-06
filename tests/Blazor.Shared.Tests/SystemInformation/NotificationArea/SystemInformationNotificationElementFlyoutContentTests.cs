using Blazor.Shared.Services;
using Blazor.Shared.SystemInformation.Extensions;
using Blazor.Shared.SystemInformation.NotificationArea;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Sdk.Client.Infrastructure;
using Sdk.Instance;
using Xunit;

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
            .AddSingleton(Substitute.For<ISuiteControlService>());

        // Act
        var component = ctx.Render<SystemInformationNotificationElementFlyoutContent>();

        // Assert
        Assert.NotNull(component);
    }
}
