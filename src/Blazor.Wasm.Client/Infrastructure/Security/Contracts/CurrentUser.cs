namespace Blazor.Wasm.Client.Infrastructure.Security.Contracts;

public sealed class CurrentUser
{
    public bool IsAuthenticated { get; set; }
    public string? UserName { get; set; }
    public Dictionary<string, string>? Claims { get; set; }
}
