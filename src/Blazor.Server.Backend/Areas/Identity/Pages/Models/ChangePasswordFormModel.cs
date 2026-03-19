using System.ComponentModel.DataAnnotations;
using Blazor.Server.Backend.Localization;
using ViciOne.Ui.Localization.Resources;

namespace Blazor.Server.Backend.Areas.Identity.Pages.Models;

public sealed class ChangePasswordFormModel
{
    [Display(ResourceType = typeof(CommonVocabulary), Name = nameof(CommonVocabulary.User))]
    [Required(AllowEmptyStrings = false, ErrorMessageResourceType = typeof(ValidationMessages), ErrorMessageResourceName = nameof(ValidationMessages.FieldIsRequired))]
    public string Username { get; set; } = string.Empty;

    [Display(ResourceType = typeof(CommonVocabulary), Name = nameof(CommonVocabulary.Password))]
    [Required(AllowEmptyStrings = false, ErrorMessageResourceType = typeof(ValidationMessages), ErrorMessageResourceName = nameof(ValidationMessages.FieldIsRequired))]
    [DataType(DataType.Password)]
    public string Password { get; set; } = string.Empty;

    [Display(ResourceType = typeof(Common), Name = nameof(Common.NewPassword))]
    [Required(AllowEmptyStrings = false, ErrorMessageResourceType = typeof(ValidationMessages), ErrorMessageResourceName = nameof(ValidationMessages.FieldIsRequired))]
    [DataType(DataType.Password)]
    public string NewPassword { get; set; } = string.Empty;

    [Display(ResourceType = typeof(Common), Name = nameof(Common.ConfirmNewPassword))]
    [Required(AllowEmptyStrings = false, ErrorMessageResourceType = typeof(ValidationMessages), ErrorMessageResourceName = nameof(ValidationMessages.FieldIsRequired))]
    [DataType(DataType.Password)]
    public string ConfirmNewPassword { get; set; } = string.Empty;
}
