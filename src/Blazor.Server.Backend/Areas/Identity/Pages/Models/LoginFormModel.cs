using System.ComponentModel.DataAnnotations;
using System.Runtime.Serialization;
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

    [IgnoreDataMember]
    public bool RememberMe { get; set; }

    public string? RememberMePostBackValue
    {
        get;
        set
        {
            // when the checkbox is not checked, no value is posted back. To ensure that the value for remember me is correctly set,
            // we check if we get a value after the form post.
            RememberMe = !string.IsNullOrEmpty(value);
            field = value;
        }
    }
}
