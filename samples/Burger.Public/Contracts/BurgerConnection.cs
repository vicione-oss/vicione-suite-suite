using Sdk.Connections.Contracts;

namespace Burger.Public.Contracts;

public sealed class BurgerConnection : IConnection
{
    public int Patties { get; set; }
    public string PattyType { get; set; } = string.Empty;
    public string BunType { get; set; } = string.Empty;
    public bool Cheese { get; set; }
}
