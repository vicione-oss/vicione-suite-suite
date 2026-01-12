using System.ComponentModel.DataAnnotations;
using Blazor.Server.Backend.Areas.Identity.Pages.Account.Localization;
using Blazor.Server.Backend.Areas.Identity.Pages.Models.Localization;
using ViciOne.Ui.Localization.Resources;

namespace Blazor.Server.Backend.Areas.Identity.Pages.Models;

public sealed class RegisterFormModel
{
    [Display(ResourceType = typeof(CommonVocabulary), Name = nameof(CommonVocabulary.Email))]
    [Required(AllowEmptyStrings = false, ErrorMessageResourceType = typeof(ValidationMessages), ErrorMessageResourceName = nameof(ValidationMessages.FieldIsRequired))]
    [EmailAddress(ErrorMessageResourceType = typeof(Validation), ErrorMessageResourceName = nameof(Validation.ErrorInputNotAEmailAdress))]
    public string Email { get; set; } = string.Empty;

    [Display(ResourceType = typeof(CommonVocabulary), Name = nameof(CommonVocabulary.Password))]
    [DataType(DataType.Password)]
    [Required(AllowEmptyStrings = false, ErrorMessageResourceType = typeof(ValidationMessages), ErrorMessageResourceName = nameof(ValidationMessages.FieldIsRequired))]
    public string Password { get; set; } = string.Empty;

    [Display(ResourceType = typeof(Common), Name = nameof(Common.ConfirmPassword))]
    [DataType(DataType.Password)]
    [Required(AllowEmptyStrings = false, ErrorMessageResourceType = typeof(ValidationMessages), ErrorMessageResourceName = nameof(ValidationMessages.FieldIsRequired))]
    public string ConfirmPassword { get; set; } = string.Empty;
}
