using Blazor.Shared.Connections.ControlPanels.Connections.Extensions;
using Blazor.Shared.Tests.Connections.Extensions;
using Sdk.Client.Connections;
using Sdk.Connections.Contracts;

namespace Blazor.Shared.Tests.Connections.ControlPanels.Extensions;

public sealed class IConnectionTypeRegistryExtensionsTests
{
    [Fact]
    public void Should_sort_connection_type_combo_box_items_by_display_name()
    {
        // Arrange
        var registry = Substitute.For<IConnectionTypeRegistry>();
        registry.GetConnectionTypes().Returns(["postgres", "sqlite", "opcua-server", "http", "opcua-client", "mqtt"]);

        var uiRegistry = Substitute.For<IConnectionTypeUiRegistry>().Setup(new Dictionary<string, string>
        {
            ["postgres"] = "Postgres",
            ["sqlite"] = "SQLite",
            ["opcua-server"] = "OPC-UA-Server",
            ["http"] = "HTTP",
            ["opcua-client"] = "OPC-UA-Client",
            ["mqtt"] = "MQTT",
        });

        // Act
        var items = registry.GetConnectionTypeComboBoxItems(uiRegistry);

        // Assert
        items.Select(i => i.Text).Should().Equal("HTTP", "MQTT", "OPC-UA-Client", "OPC-UA-Server", "Postgres", "SQLite");
    }
}
