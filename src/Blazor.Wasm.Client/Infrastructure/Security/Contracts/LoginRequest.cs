using System.ComponentModel.DataAnnotations;

namespace Blazor.Wasm.Client.Infrastructure.Security.Contracts;

public sealed class LoginRequest
{
    [Required]
    public string? UserName { get; set; }
    [Required]
    public string? Password { get; set; }
    public bool RememberMe { get; set; }
}
