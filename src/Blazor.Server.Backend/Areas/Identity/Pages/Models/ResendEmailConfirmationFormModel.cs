using System.ComponentModel.DataAnnotations;
using Blazor.Server.Backend.Areas.Identity.Pages.Models.Localization;
using ViciOne.Ui.Localization.Resources;

namespace Blazor.Server.Backend.Areas.Identity.Pages.Models;

public sealed class ResendEmailConfirmationFormModel
{
    [Display(ResourceType = typeof(CommonVocabulary), Name = nameof(CommonVocabulary.Email))]
    [Required(AllowEmptyStrings = false, ErrorMessageResourceType = typeof(ValidationMessages), ErrorMessageResourceName = nameof(ValidationMessages.FieldIsRequired))]
    [EmailAddress(ErrorMessageResourceType = typeof(ModelValidation), ErrorMessageResourceName = nameof(ModelValidation.ErrorInputNotAEmailAdress))]
    public string Email { get; set; } = string.Empty;
}
