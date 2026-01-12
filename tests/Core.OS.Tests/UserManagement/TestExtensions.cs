using Core.OS.UserManagement.Extensions;
using Core.Shared.UserManagement.Contracts;
using Sdk;

namespace Core.OS.Tests.UserManagement;

internal static class TestExtensions
{
    public static UserProfile ToUserProfile(this SeedingExtensions.SeedUser seedUser, bool setCurrentPassword = false)
    {
        var suiteUser = SeedingExtensions.CreateSuiteUserWithDefaults(seedUser);
        return new UserProfile
        {
            // required:
            UserName = suiteUser.UserName is not null ? new UserName(suiteUser.UserName) : UserName.Empty,
            Email = suiteUser.Email!,
            Roles = [new Role(SeedingExtensions.GetRoleNameByConvention(Constants.SystemModuleId, seedUser.AccessLevel))],

            // optional:
            Firstname = suiteUser.FirstName,
            Lastname = suiteUser.LastName,
            Title = suiteUser.Title,
            Occupation = suiteUser.Occupation,
            Department = suiteUser.Department,
            Language = suiteUser.Language,
            TimeZone = suiteUser.TimeZone,
            Street = suiteUser.Street,
            StreetNumber = suiteUser.StreetNumber,
            ZipCode = suiteUser.ZipCode,
            City = suiteUser.City,
            Country = suiteUser.Country,
            PhoneNumber = suiteUser.PhoneNumber,
            Mobile = suiteUser.Mobile,
            CurrentPassword = setCurrentPassword ? seedUser.Password : null
        };
    }
}
