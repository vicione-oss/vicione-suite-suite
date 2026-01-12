using Core.OS.DbContext;
using Core.OS.UserManagement.Extensions;
using Core.Shared.UserManagement.Contracts;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Sdk.Authorization;
using Xunit;

namespace Core.OS.Tests.UserManagement;

internal static class TestExtensions
{
    public const string TestRoleName = "Tester";

    public static UserProfile ToUserProfile(this SeedingExtensions.SeedUser seedUser, bool setCurrentPassword = false)
    {
        var suiteUser = SeedingExtensions.CreateSuiteUserWithDefaults(seedUser);
        return new UserProfile
        {
            // required:
            UserName = suiteUser.UserName is not null ? new UserName(suiteUser.UserName) : UserName.Empty,
            Email = suiteUser.Email!,
            Roles = [TestRoleName],

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

    public static SqliteConnection CreateSqliteMemoryConnection() => new("Data Source=:memory:");

    public static UserDbContextSqlite CreateUserDbContextSqlite(SqliteConnection connection)
    {
        connection.Open();

        var dbOptions = new DbContextOptionsBuilder<UserDbContextSqlite>().UseSqlite(connection).Options;
        var dbContext = Activator.CreateInstance(typeof(UserDbContextSqlite), dbOptions) as UserDbContextSqlite;

        Assert.NotNull(dbContext);

        dbContext.Database.EnsureDeleted();
        dbContext.Database.EnsureCreated();

        return dbContext;
    }

    /// <summary>
    /// Will create a role per module per access level and assign all respective claims to that role
    /// </summary>
    internal static async Task SeedTestRole(this IServiceProvider services)
    {
        var roleManager = services.GetRequiredService<RoleManager<SuiteRole>>();
        var moduleAuthorizationClaimParser = services.GetRequiredService<IModuleAuthorizationClaimParser>();
        var features = services.GetServices<IModuleFeature>();
        var testerRole = await roleManager.FindByNameAsync(TestRoleName).ConfigureAwait(false);

        if (testerRole is null)
        {
            // Delete roles created before there was a sysadmin - can be removed after a while
            foreach (var existingRole in roleManager.Roles)
                await roleManager.DeleteAsync(existingRole).ConfigureAwait(false);

            testerRole = new SuiteRole(TestRoleName)
            {
                Description = "The tester role with partial access to features.",
            };
            _ = await roleManager.CreateAsync(testerRole).ConfigureAwait(false);
        }

        foreach (var feature in features)
        {
            var assignedClaims = await roleManager.GetClaimsAsync(testerRole);
            if (!assignedClaims.Any(claim
                    => moduleAuthorizationClaimParser.TryParse(claim, out var claimValue)
                       && claimValue.ModuleId == feature.ModuleId
                       && claimValue.AccessLevel == AccessLevel.Full
                       && claimValue.FeatureName == feature.Name))
            {
                var newClaim = ModuleAuthorizationClaimFactory.CreateClaim(feature.ModuleId, AccessLevel.Partial, feature.Name);
                _ = await roleManager.AddClaimAsync(testerRole, newClaim);
            }
        }

        var userManager = services.GetRequiredService<UserManager<SuiteUser>>();
        foreach (var user in SeedingExtensions.Users)
        {
            var suiteUser = await userManager.FindByNameAsync(user.UserName);
            if (suiteUser is null)
                continue;

            _ = await userManager.AddToRoleAsync(suiteUser, TestRoleName);
        }
    }
}
