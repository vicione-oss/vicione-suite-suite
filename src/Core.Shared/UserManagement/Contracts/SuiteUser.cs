using Microsoft.AspNetCore.Identity;

namespace Core.Shared.UserManagement.Contracts;

public class SuiteUser : IdentityUser
{
    public string? LastName { get; set; }
    public string? FirstName { get; set; }
    public string? Title { get; set; }
    public string? Occupation { get; set; }
    public string? Department { get; set; }
    public string? Street { get; set; }
    public string? StreetNumber { get; set; }
    public string? ZipCode { get; set; }
    public string? City { get; set; }
    public string? Country { get; set; }
    public string? Mobile { get; set; }
    public string? Language { get; set; }
    public string? TimeZone { get; set; }
    public DateTimeOffset? PasswordExpirationDate { get; set; }
}
