using Core.Shared.UserManagement.Contracts;
using Core.Shared.UserManagement.Extensions;
using Microsoft.AspNetCore.Identity;
using Sdk.UserManagement.Contracts;

namespace Core.OS.UserManagement.Extensions;

public static class UserProfileExtensions
{
    extension(SuiteUser user)
    {
        public async Task<UserProfile> CreateUserProfile(UserManager<SuiteUser> userManager)
            => new()
            {
                // Required:
                UserName = user.UserName is not null ? new UserName(user.UserName) : UserName.Empty,
                Email = user.Email!,
                Roles = [.. await userManager.GetRolesAsync(user)],
                Claims = [.. (await userManager.GetClaimsAsync(user)).Select(claim => claim.ToUserManagementClaim())],

                // Optional:
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

        public UserInformation ToUserInformation()
        {
            // UserName and Email will always be filled except during creation,
            // but the compiler does not know that, so we need to check for null here.
            ArgumentNullException.ThrowIfNull(user.UserName);
            ArgumentNullException.ThrowIfNull(user.Email);

            return new(user.UserName, user.Email, user.FirstName, user.LastName);
        }

        public void AssignOptionalData(UserProfile userProfile)
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
}
