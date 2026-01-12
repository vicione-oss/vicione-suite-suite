using System.ComponentModel.DataAnnotations;
using ViciOne.Ui.Localization.Resources;

namespace Blazor.Server.Backend.Areas.Identity.Pages.Models;

public sealed class LoginFormModel
{
    [Display(ResourceType = typeof(CommonVocabulary), Name = nameof(CommonVocabulary.User))]
    [Required(AllowEmptyStrings = false, ErrorMessageResourceType = typeof(ValidationMessages), ErrorMessageResourceName = nameof(ValidationMessages.FieldIsRequired))]
    public string Username { get; set; } = string.Empty;

    [Display(ResourceType = typeof(CommonVocabulary), Name = nameof(CommonVocabulary.Password))]
    [DataType(DataType.Password)]
    [Required(AllowEmptyStrings = false, ErrorMessageResourceType = typeof(ValidationMessages), ErrorMessageResourceName = nameof(ValidationMessages.FieldIsRequired))]
    public string Password { get; set; } = string.Empty;

    // Attribute 'Display' is only used for the old Identity Model. Can be removed, when the new Identity Model is used.
    // When removed, the access modifier of 'Account.Localization.Login' can be set to 'internal' again.
    [Display(ResourceType = typeof(Account.Localization.Login), Name = nameof(Account.Localization.Login.RememberMe))]
    public bool RememberMe { get; set; }
}
