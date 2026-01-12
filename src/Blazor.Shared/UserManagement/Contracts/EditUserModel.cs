using Core.Shared.UserManagement.Contracts;

namespace Blazor.Shared.UserManagement.Contracts;

public sealed class EditUserModel
{
    public UserName Username { get; set; }
    public string Email { get; set; } = string.Empty;
    public string NewPassword { get; set; } = string.Empty;
    public string CurrentPassword { get; set; } = string.Empty;

    public List<string> Roles { get; set; } = [];
    public bool IsPasswordRequired { get; set; }
}
