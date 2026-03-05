using System.ComponentModel.DataAnnotations;

namespace Blazor.Server.Backend.Areas.Identity.Pages.Models;

public sealed class ForgotPasswordFormModel
{
    [Required]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;
}
