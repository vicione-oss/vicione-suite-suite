using System.ComponentModel.DataAnnotations;
using Blazor.Server.Backend.Localization;
using ViciOne.Ui.Localization.Resources;

namespace Blazor.Server.Backend.Areas.Identity.Pages.Models;

public sealed class SetPasswordFormModel
{
    [Display(ResourceType = typeof(Common), Name = nameof(Common.NewPassword))]
    [Required(AllowEmptyStrings = false, ErrorMessageResourceType = typeof(ValidationMessages), ErrorMessageResourceName = nameof(ValidationMessages.FieldIsRequired))]
    [DataType(DataType.Password)]
    public string NewPassword { get; set; } = string.Empty;

    [Display(ResourceType = typeof(Common), Name = nameof(Common.ConfirmPassword))]
    [Required(AllowEmptyStrings = false, ErrorMessageResourceType = typeof(ValidationMessages), ErrorMessageResourceName = nameof(ValidationMessages.FieldIsRequired))]
    [DataType(DataType.Password)]
    public string ConfirmPassword { get; set; } = string.Empty;
}
