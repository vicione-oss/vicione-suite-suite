using System.ComponentModel.DataAnnotations;

namespace Core.OS.UserManagement.Configuration;

public sealed class UserManagementOptions
{
    public const string ConfigSection = "UserManagement";

    public bool SeedTestUsers { get; set; }

    [StringLength(64)]
    public string AdministratorName { get; set; } = "Administrator";

    [StringLength(32)]
    [MinLength(12)]
    public string InitialAdministratorPassword { get; set; } = "Pa$$w0rd1234";
    // By default, Identity requires that passwords contain an uppercase character, lowercase character,
    // a digit, and a non-alphanumeric character. Passwords must be at least 12 characters long.
}
