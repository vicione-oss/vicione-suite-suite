using Blazor.Shared.Services;
using Blazor.Shared.SystemInformation.Components;
using Blazor.Shared.SystemInformation.Services;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;
using NSubstitute;
using NSubstitute.ReceivedExtensions;
using Sdk.Client.Services;
using Xunit;

namespace Blazor.Shared.Tests.SystemInformation.Components;

public class SystemMonitoringComponentTests
{
    private readonly IJsInterop _jsInterop = Substitute.For<IJsInterop>();

    [Fact]
    public void Should_render_component()
    {
        // Arrange
        using var ctx = new TestContext();
        var timeProvider = Substitute.For<TimeProvider>();
        timeProvider.LocalTimeZone.Returns(TimeZoneInfo.Utc);

        ctx.Services
            .AddSingleton(_jsInterop)
            .AddSingleton<MonitoringService>()
            .AddSingleton<ISuiteControlService>(Substitute.For<ISuiteControlService>())
            .AddKeyedScoped(Sdk.Constants.ClientTimeProviderServiceKey, (_, __) => timeProvider);

        // Act
        var component = ctx.RenderComponent<SystemMonitoringComponent>();

        // Assert
        Assert.NotNull(component);
    }

    [Fact]
    public async Task Should_dispose_js_module_reference()
    {
        // Arrange
        var _jsModuleReference = Substitute.For<IJSObjectReference>();
        var timeProvider = Substitute.For<TimeProvider>();
        timeProvider.LocalTimeZone.Returns(TimeZoneInfo.Utc);

        using (var ctx = new TestContext())
        {
            ctx.Services
            .AddSingleton(_jsInterop)
            .AddSingleton<MonitoringService>()
            .AddSingleton<ISuiteControlService>(Substitute.For<ISuiteControlService>())
            .AddKeyedScoped<TimeProvider>(Sdk.Constants.ClientTimeProviderServiceKey, (_, __) => timeProvider);

            _jsInterop.IncludeModuleScript<SharedClientModule>("system-monitoring-component.js").Returns(_jsModuleReference);

            // Act
            var component = ctx.RenderComponent<SystemMonitoringComponent>();
            Assert.NotNull(component);
        }

        // Assert
        await _jsModuleReference.Received(1).DisposeAsync();
    }
}
