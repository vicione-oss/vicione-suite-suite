using System.ComponentModel.DataAnnotations;
using Blazor.Server.Backend.Areas.Identity.Pages.Models.Localization;
using Blazor.Server.Backend.Localization;
using ViciOne.Ui.Localization.Resources;

namespace Blazor.Server.Backend.Areas.Identity.Pages.Models;

public sealed class ResetPasswordFormModel
{
    [Display(ResourceType = typeof(CommonVocabulary), Name = nameof(CommonVocabulary.Email))]
    [Required(AllowEmptyStrings = false, ErrorMessageResourceType = typeof(ValidationMessages), ErrorMessageResourceName = nameof(ValidationMessages.FieldIsRequired))]
    [EmailAddress(ErrorMessageResourceType = typeof(ModelValidation), ErrorMessageResourceName = nameof(ModelValidation.ErrorInputNotAEmailAdress))]
    public string Email { get; set; } = string.Empty;

    [Display(ResourceType = typeof(Common), Name = nameof(Common.NewPassword))]
    [Required(AllowEmptyStrings = false, ErrorMessageResourceType = typeof(ValidationMessages), ErrorMessageResourceName = nameof(ValidationMessages.FieldIsRequired))]
    [DataType(DataType.Password)]
    public string NewPassword { get; set; } = string.Empty;

    [Display(ResourceType = typeof(Common), Name = nameof(Common.ConfirmNewPassword))]
    [Required(AllowEmptyStrings = false, ErrorMessageResourceType = typeof(ValidationMessages), ErrorMessageResourceName = nameof(ValidationMessages.FieldIsRequired))]
    [DataType(DataType.Password)]
    public string ConfirmNewPassword { get; set; } = string.Empty;
}
