namespace Blazor.Shared.Network.ControlPanels.Proxies.Models;

public sealed class ProxySettings
{
    public bool Enabled { get; set; }
    public string? Server { get; set; }
    public string? Port { get; set; }
    public bool PasswordRequired { get; set; }
    public string? Username { get; set; }
    public string? Password { get; set; }
}
