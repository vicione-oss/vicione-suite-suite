using System.ComponentModel.DataAnnotations;
using ViciOne.Ui.Localization.Resources;

namespace Blazor.Server.Backend.Areas.Identity.Pages.Models;

public sealed class ForgotPasswordFormModel
{
    [Display(ResourceType = typeof(CommonVocabulary), Name = nameof(CommonVocabulary.Email))]
    [Required]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;
}
