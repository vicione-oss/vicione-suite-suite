using AwesomeAssertions;
using Blazor.Shared.EnvironmentOverrides.ControlPanels.Components;
using Blazor.Shared.EnvironmentOverrides.ControlPanels.Extensions;
using Blazor.Shared.Settings.Extensions;
using Core.Shared.EnvironmentOverrides;
using Microsoft.Extensions.DependencyInjection;
using Sdk.Client.ControlPanels.Services;
using Xunit;

namespace Blazor.Shared.Tests.EnvironmentOverrides.ControlPanels.Services;

public sealed class EnvironmentOverridesControlPanelGateTests
{
    /// <summary>
    /// Switches the overrides through the process environment, which the whole assembly shares.
    /// Nothing else in it reads that variable, and xUnit runs the cases of one class in sequence,
    /// so restoring it on disposal is enough.
    /// </summary>
    public sealed class Execute : IDisposable
    {
        private readonly string? _previousEnv =
            Environment.GetEnvironmentVariable(EnvironmentOverridesSwitch.EnabledEnvironmentVariable);

        public void Dispose() => Switch(_previousEnv);

        [Fact]
        public async Task Should_remove_the_control_panel_when_the_overrides_are_switched_off()
        {
            // Arrange
            Switch(null);
            await using var services = SetupServices();

            // Act
            await ExecuteHandlers(services);

            // Assert
            GetEnvironmentOverridesControlPanels(services).Should().BeEmpty();
        }

        [Fact]
        public async Task Should_keep_the_control_panel_when_the_overrides_are_switched_on()
        {
            // Arrange
            Switch("true");
            await using var services = SetupServices();

            // Act
            await ExecuteHandlers(services);

            // Assert
            GetEnvironmentOverridesControlPanels(services).Should().ContainSingle();
        }

        private static void Switch(string? configured)
            => Environment.SetEnvironmentVariable(EnvironmentOverridesSwitch.EnabledEnvironmentVariable,
                configured);

        private static ServiceProvider SetupServices()
            => new ServiceCollection()
                .AddControlPanelInfrastructure()
                .AddEnvironmentOverridesControlPanel()
                .BuildServiceProvider();

        private static async Task ExecuteHandlers(IServiceProvider services)
        {
            foreach (var handler in services.GetServices<IUpdateControlPanelRegistryHandler>())
                await handler.Execute(TestContext.Current.CancellationToken);
        }

        private static IEnumerable<IControlPanelRegistryItem> GetEnvironmentOverridesControlPanels(IServiceProvider services)
            => services.GetRequiredService<IControlPanelRegistry<SharedClientModule>>()
                .Where(i => i.ComponentType == typeof(EnvironmentOverridesControlPanel));
    }
}
