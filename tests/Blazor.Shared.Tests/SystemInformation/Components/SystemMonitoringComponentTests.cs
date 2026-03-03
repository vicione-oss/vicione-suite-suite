using Blazor.Shared.Services;
using Blazor.Shared.SystemInformation.Components;
using Blazor.Shared.SystemInformation.Services;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;
using NSubstitute;
using Sdk.Client.Services;
using Xunit;

namespace Blazor.Shared.Tests.SystemInformation.Components;

public class SystemMonitoringComponentTests
{
    private readonly IJsInterop _jsInterop = Substitute.For<IJsInterop>();

    [Fact]
    public async Task Should_render_component()
    {
        // Arrange
        await using var ctx = new BunitContext();
        var timeProvider = Substitute.For<TimeProvider>();
        timeProvider.LocalTimeZone.Returns(TimeZoneInfo.Utc);

        ctx.Services
            .AddSingleton(_jsInterop)
            .AddSingleton<MonitoringService>()
            .AddSingleton(Substitute.For<ISuiteControlService>())
            .AddKeyedScoped(Sdk.Constants.ClientTimeProviderServiceKey, (_, _) => timeProvider);

        // Act
        var component = ctx.Render<SystemMonitoringComponent>();

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

        await using (var ctx = new BunitContext())
        {
            ctx.Services
            .AddSingleton(_jsInterop)
            .AddSingleton<MonitoringService>()
            .AddSingleton(Substitute.For<ISuiteControlService>())
            .AddKeyedScoped<TimeProvider>(Sdk.Constants.ClientTimeProviderServiceKey, (_, _) => timeProvider);

            _jsInterop.IncludeModuleScript<SharedClientModule>("system-monitoring-component.js", Arg.Any<CancellationToken>()).Returns(_jsModuleReference);

            // Act
            var component = ctx.Render<SystemMonitoringComponent>();
            Assert.NotNull(component);
        }

        // Assert
        await _jsModuleReference.Received(1).DisposeAsync();
    }
}
