using System.ComponentModel.DataAnnotations;
using ViciOne.Ui.Localization.Resources;

namespace Blazor.Server.Backend.Areas.Identity.Pages.Models;

public sealed class ForgotPasswordFormModel
{
    [Display(ResourceType = typeof(CommonVocabulary), Name = nameof(CommonVocabulary.User))]
    [Required(AllowEmptyStrings = false, ErrorMessageResourceType = typeof(ValidationMessages), ErrorMessageResourceName = nameof(ValidationMessages.FieldIsRequired))]
    public string Username { get; set; } = string.Empty;
}
