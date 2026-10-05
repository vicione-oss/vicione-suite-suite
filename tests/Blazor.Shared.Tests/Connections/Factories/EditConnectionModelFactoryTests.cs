using Blazor.Shared.Connections.Factories;
using Blazor.Shared.Tests.Connections.Extensions;
using Sdk.Client.Connections;
using Sdk.Connections.Contracts;

namespace Blazor.Shared.Tests.Connections.Factories;

public sealed class EditConnectionModelFactoryTests
{
    [Fact]
    public void Should_default_new_connection_to_first_connection_type_by_display_name()
    {
        // Arrange
        var registry = Substitute.For<IConnectionTypeRegistry>().Setup();
        registry.GetConnectionTypes().Returns(["sqlite", "mqtt", "http"]);

        var uiRegistry = Substitute.For<IConnectionTypeUiRegistry>().Setup(new Dictionary<string, string>
        {
            ["sqlite"] = "SQLite",
            ["mqtt"] = "MQTT",
            ["http"] = "HTTP",
        });

        // Act
        var model = EditConnectionModelFactory.CreateNew(registry, uiRegistry);

        // Assert
        model.Type.Should().Be(new ConnectionType("http"));
    }
}
