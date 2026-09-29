using Blazor.Shared.Connections.Components;
using Blazor.Shared.Connections.Factories;
using Blazor.Shared.Connections.Services;
using Blazor.Shared.Settings.Extensions;
using Blazor.Shared.Tests.Connections.Extensions;
using Blazor.Tests.Tools;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Sdk.Client.Components.Settings;
using Sdk.Client.Infrastructure;
using Sdk.Connections.Contracts;

namespace Blazor.Shared.Tests.Connections.Components;

public sealed class TestSettingsGroupTests
{
    [Fact]
    public async Task Should_show_the_test_button_as_busy_while_the_test_runs()
    {
        // Arrange
        await using var ctx = new BunitContext();

        var registry = Substitute.For<IConnectionTypeRegistry>().Setup();

        ctx.Services.AddSingleton(registry);
        ctx.Services.AddScoped<TestConnectionService>();

        ctx.SetupBlazorSharedSettings(setup =>
            setup.ClientMediator.Register(Arg.Any<IEventConsumer<Core.Shared.Connections.Events.TestConnectionDoneEvent>>())
                .Returns(Substitute.For<IDisposable>()));

        var model = EditConnectionModelFactory.CreateNew(registry);

        var component = ctx.Render<TestSettingsGroup>(builder => builder.Add(p => p.Model, model));

        // Act
        await component.FindComponent<SettingsFieldButton>().Find("button").ClickAsync();

        // Assert
        component.WaitForAssertion(() =>
        {
            var testButton = component.FindComponent<SettingsFieldButton>().Instance;

            testButton.Busy.Should().BeTrue();
            testButton.Enabled.Should().BeTrue();
        });
    }
}
