using Microsoft.AspNetCore.Identity;

namespace Core.Shared.UserManagement.Contracts;

public sealed class SuiteRole : IdentityRole
{
    public string? Description { get; set; }
    public bool? Managed { get; set; }

    public SuiteRole(string name)
    {
        Name = name;
        ConcurrencyStamp = Guid.NewGuid().ToString("D");
    }
}
