using Blazor.Shared.Connections.Factories;
using Sdk.Client.Connections;
using Sdk.Connections.Contracts;
using ViciOne.Ui.Blazor.Components.ComboBox;

namespace Blazor.Shared.Connections.Components;

public sealed partial class DatabaseSettings : ConnectionSettingsComponentBase<DatabaseConnection>
{
    private readonly List<ComboBoxItem<DatabaseConnectionType, string>> _connectionTypes = ComboBoxItemFactory.GetDatabaseConnectionTypes();
}
