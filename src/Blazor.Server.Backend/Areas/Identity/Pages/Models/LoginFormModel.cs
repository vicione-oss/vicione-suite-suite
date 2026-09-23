using System.ComponentModel.DataAnnotations;
using System.Runtime.Serialization;
using Blazor.Shared.Profile.Models;
using ViciOne.Ui.Localization.Resources;

namespace Blazor.Server.Backend.Areas.Identity.Pages.Models;

public sealed class LoginFormModel
{
    [Required(AllowEmptyStrings = false, ErrorMessageResourceType = typeof(ValidationMessages), ErrorMessageResourceName = nameof(ValidationMessages.FieldIsRequired))]
    public string Username { get; set; } = string.Empty;

    [Display(ResourceType = typeof(CommonVocabulary), Name = nameof(CommonVocabulary.Password))]
    [DataType(DataType.Password)]
    [Required(AllowEmptyStrings = false,
        ErrorMessageResourceType = typeof(ValidationMessages),
        ErrorMessageResourceName = nameof(ValidationMessages.FieldIsRequired))]
    public string Password { get; set; } = string.Empty;

    [IgnoreDataMember]
    public bool RememberMe { get; set; }

    public PasskeyInputModel? Passkey { get; set; }

    public string? RememberMePostBackValue
    {
        get;
        set
        {
            // An unchecked checkbox posts no value at all, so presence of a value is what sets RememberMe.
            RememberMe = !string.IsNullOrEmpty(value);
            field = value;
        }
    }
}
