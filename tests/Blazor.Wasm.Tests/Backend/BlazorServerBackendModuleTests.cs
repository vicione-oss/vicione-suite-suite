using Blazor.DevAssets;
using Blazor.Wasm.Backend;
using Blazor.Wasm.Client.Infrastructure.SignalR;
using Microsoft.Extensions.DependencyInjection;
using Sdk.Testing.Backend;
using Xunit;

namespace Blazor.Wasm.Tests.Backend;

public class BlazorServerBackendModuleTests
{
    [Fact]
    public void Init_module_should_register_and_configure_services()
    {
        // Arrange
        var module = new BlazorWasmBackendModule();

        // Act
        var serviceProvider = module.TestModuleInitialization(services =>
        {
            var testOptions = new UiHostCircuitOptions();

            var customSettings = new Dictionary<string, string?>
            {
                { nameof(testOptions.EnableDetailedErrors), $"{testOptions.EnableDetailedErrors}" },
                { nameof(testOptions.MaximumReceiveMessageSize), $"{testOptions.MaximumReceiveMessageSize}" }
            };

            var testConfig = new TestConfig();
            testConfig.AddModule("Blazor.Wasm");
            testConfig.AddCustomSettings(customSettings);

            services.AddConfiguration(testConfig);
        });

        // Assert
        Assert.NotNull(module.ModuleKey.ModuleId);
        Assert.NotNull(serviceProvider.GetRequiredService<IMessageHub>());
    }
}
