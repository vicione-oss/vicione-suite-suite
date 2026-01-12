namespace Core.Shared.UserManagement.Contracts;

public sealed class UserProfile
{
    public required UserName UserName { get; set; }
    public string? CurrentPassword { get; set; }
    public string? NewPassword { get; set; }

    public string? Lastname { get; set; }
    public string? Firstname { get; set; }
    public string? Title { get; set; }
    public string? Occupation { get; set; }
    public string? Department { get; set; }
    public string? Street { get; set; }
    public string? StreetNumber { get; set; }
    public string? ZipCode { get; set; }
    public string? City { get; set; }
    public string? Country { get; set; }
    public string? Email { get; set; }
    public string? PhoneNumber { get; set; }
    public string? Mobile { get; set; }
    public string? Language { get; set; }
    public string? TimeZone { get; set; }
    public DateTimeOffset? PasswordExpirationDate { get; set; }
    public List<Role> Roles { get; set; } = [];
    public List<UserProfileClaim> Claims { get; set; } = [];
}
