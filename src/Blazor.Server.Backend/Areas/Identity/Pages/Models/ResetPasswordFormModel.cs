using System.ComponentModel.DataAnnotations;
using Blazor.Server.Backend.Areas.Identity.Pages.Models.Localization;
using ViciOne.Ui.Localization.Resources;

namespace Blazor.Server.Backend.Areas.Identity.Pages.Models;

public sealed class ResetPasswordFormModel
{
    [Required(AllowEmptyStrings = false, ErrorMessageResourceType = typeof(ValidationMessages), ErrorMessageResourceName = nameof(ValidationMessages.FieldIsRequired))]
    [EmailAddress(ErrorMessageResourceType = typeof(Validation), ErrorMessageResourceName = nameof(Validation.ErrorInputNotAEmailAdress))]
    public string Email { get; set; } = string.Empty;

    [Required(AllowEmptyStrings = false, ErrorMessageResourceType = typeof(ValidationMessages), ErrorMessageResourceName = nameof(ValidationMessages.FieldIsRequired))]
    [DataType(DataType.Password)]
    public string NewPassword { get; set; } = string.Empty;

    [Required(AllowEmptyStrings = false, ErrorMessageResourceType = typeof(ValidationMessages), ErrorMessageResourceName = nameof(ValidationMessages.FieldIsRequired))]
    [DataType(DataType.Password)]
    public string ConfirmNewPassword { get; set; } = string.Empty;
}
