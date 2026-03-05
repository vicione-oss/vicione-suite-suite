using System.ComponentModel.DataAnnotations;
using Blazor.Server.Backend.Areas.Identity.Pages.Models.Localization;
using ViciOne.Ui.Localization.Resources;

namespace Blazor.Server.Backend.Areas.Identity.Pages.Models;

public sealed class ResendEmailConfirmationFormModel
{
    [Required(AllowEmptyStrings = false, ErrorMessageResourceType = typeof(ValidationMessages), ErrorMessageResourceName = nameof(ValidationMessages.FieldIsRequired))]
    [EmailAddress(ErrorMessageResourceType = typeof(Validation), ErrorMessageResourceName = nameof(Validation.ErrorInputNotAEmailAdress))]
    public string Email { get; set; } = string.Empty;
}
