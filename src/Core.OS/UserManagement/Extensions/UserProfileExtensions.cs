using Core.Shared.UserManagement.Contracts;
using Core.Shared.UserManagement.Extensions;
using Microsoft.AspNetCore.Identity;

namespace Core.OS.UserManagement.Extensions;

public static class UserProfileExtensions
{
    public static async Task<UserProfile> CreateUserProfile(this SuiteUser user, UserManager<SuiteUser> userManager)
            => new()
            {
                // required:
                UserName = user.UserName is not null ? new UserName(user.UserName) : UserName.Empty,
                Email = user.Email!,
                Roles = [.. (await userManager.GetRolesAsync(user)).Select(r => new Role(r))],
                Claims = [.. (await userManager.GetClaimsAsync(user)).Select(claim => claim.ToUserProfileClaim())],

                // optional:
                Firstname = user.FirstName,
                Lastname = user.LastName,
                Title = user.Title,
                Occupation = user.Occupation,
                Department = user.Department,
                Language = user.Language,
                TimeZone = user.TimeZone,
                Street = user.Street,
                StreetNumber = user.StreetNumber,
                ZipCode = user.ZipCode,
                City = user.City,
                Country = user.Country,
                PhoneNumber = user.PhoneNumber,
                Mobile = user.Mobile,
                PasswordExpirationDate = user.PasswordExpirationDate
            };

    public static void AssignOptionalData(this SuiteUser user, UserProfile userProfile)
    {
        user.FirstName = userProfile.Firstname;
        user.LastName = userProfile.Lastname;
        user.Title = userProfile.Title;
        user.Occupation = userProfile.Occupation;
        user.Department = userProfile.Department;
        user.Language = userProfile.Language;
        user.TimeZone = userProfile.TimeZone;
        user.Street = userProfile.Street;
        user.StreetNumber = userProfile.StreetNumber;
        user.ZipCode = userProfile.ZipCode;
        user.City = userProfile.City;
        user.Country = userProfile.Country;
        user.PhoneNumber = userProfile.PhoneNumber;
        user.Mobile = userProfile.Mobile;
        user.PasswordExpirationDate = userProfile.PasswordExpirationDate;
    }
}
