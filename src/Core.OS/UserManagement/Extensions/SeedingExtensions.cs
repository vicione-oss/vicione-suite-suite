using Core.OS.Modules;
using Core.OS.Modules.Services;
using Core.OS.UserManagement.Configuration;
using Core.Shared.UserManagement.Contracts;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using Sdk;
using Sdk.Authorization;

namespace Core.OS.UserManagement.Extensions;

public static class SeedingExtensions
{
    //Users
    internal static SeedUser Eddy => new()
    {
        Email = "Eddy@worlddomination24.com",
        UserName = "Eddy",
        Password = "Up2noGood!!!",
        AccessLevel = AccessLevel.Full,
        FirstName = "Eddy",
        LastName = "Lovecraft",
        Title = "High Lord"
    };
    internal static SeedUser Admin => new()
    {
        Email = "Admin@it-masters.com",
        UserName = "Admin",
        Password = "Up2noGood!!!",
        AccessLevel = AccessLevel.Full,
        FirstName = "Admin",
        LastName = "Master",
        Title = "Executus"
    };
    internal static SeedUser Alice => new()
    {
        Email = "AliceSmith@example.com",
        UserName = "Alice",
        Password = "Up2noGood!!!",
        AccessLevel = AccessLevel.Partial,
        FirstName = "Alice",
        LastName = "Smith"
    };
    internal static SeedUser Bob => new()
    {
        Email = "Superbob@example.com",
        UserName = "Bob",
        Password = "Up2noGood!!!",
        AccessLevel = AccessLevel.Partial,
        FirstName = "Robert",
        LastName = "van der Honigwiese"
    };
    internal static IEnumerable<SeedUser> Users =>
    [
        Alice,
        Bob,
        Eddy,
        Admin
    ];

    internal static async Task SeedUsersAndRoles(this IServiceProvider scopedServices)
    {
        var userManagementOptions = scopedServices.GetRequiredService<IOptions<UserManagementOptions>>().Value;
        var logger = scopedServices.GetRequiredService<ILogger<ApplicationWorker>>();

        await scopedServices.SeedRoles(logger).ConfigureAwait(false);

        await scopedServices.SeedTestUsers(userManagementOptions, logger).ConfigureAwait(false);

        await scopedServices.SeedAdministrator(userManagementOptions, logger).ConfigureAwait(false);
    }

    /// <summary>
    /// Will create a role per module per access level and assign all respective claims to that role
    /// </summary>
    private static async Task SeedRoles(this IServiceProvider services, ILogger logger)
    {
        var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();
        var moduleAuthorizationClaimParser = services.GetRequiredService<IModuleAuthorizationClaimParser>();
        var features = services.GetServices<IModuleFeature>();


        // Begin legacy cleanup for roles and role claims - can be removed after a while
        foreach (var existingRole in roleManager.Roles)
        {
            if (existingRole.Name?.EndsWith("Viewer", StringComparison.Ordinal) == true // old role no longer in use
                || existingRole.Name?.StartsWith(Module.Constants.BlazorServerModuleId, StringComparison.Ordinal) == true) // we use "System" for all Core permissions
                await roleManager.DeleteAsync(existingRole).ConfigureAwait(false);
        }
        // End legacy cleanup

        foreach (var feature in features)
        {
            logger.LogDebug("SeedRoles -> Process feature ModuleId = {ModuleId}, Name = {Name}, Path = {Path}", feature.ModuleId, feature.Name, feature.Path);
            foreach (var accessLevel in Enum.GetValues<AccessLevel>())
            {
                var roleName = GetRoleNameByConvention(feature.ModuleId, accessLevel);
                var dbRole = await roleManager.FindByNameAsync(roleName);
                if (dbRole == null)
                {
                    dbRole = new IdentityRole
                    {
                        Id = Guid.NewGuid().ToString(),
                        Name = roleName,
                        ConcurrencyStamp = Guid.NewGuid().ToString("D")
                    };

                    _ = await roleManager.CreateAsync(dbRole);
                }

                var assignedClaims = await roleManager.GetClaimsAsync(dbRole);
                if (!assignedClaims.Any(claim
                        => moduleAuthorizationClaimParser.TryParse(claim, out var claimValue)
                           && claimValue.ModuleId == feature.ModuleId
                           && claimValue.AccessLevel == accessLevel
                           && claimValue.FeatureName == feature.Name))
                {

                    logger.LogDebug("SeedRoles -> Create claim ModuleId = {ModuleId}, Name = {Name}, Path = {Path}", feature.ModuleId, feature.Name, feature.Path);
                    var newClaim = ModuleAuthorizationClaimFactory.CreateClaim(feature.ModuleId, accessLevel, feature.Name);
                    _ = await roleManager.AddClaimAsync(dbRole, newClaim);
                }
            }
        }
    }

    private static async Task SeedTestUsers(this IServiceProvider services, UserManagementOptions options, ILogger logger)
    {
        if (!options.SeedTestUsers)
            return;

        var userManager = services.GetRequiredService<UserManager<SuiteUser>>();
        var moduleIds = services.GetRequiredService<IModuleHost>()
            .GetModules()
            .Select(m => m.ModuleKey.ModuleId)
            .Except([Module.Constants.BlazorServerModuleId])
            .Union([Constants.SystemModuleId])
            .ToArray();

        foreach (var user in Users)
        {
            var suiteUser = await userManager.FindByNameAsync(user.UserName);
            if (suiteUser is null)
            {
                suiteUser = CreateSuiteUserWithDefaults(user);

                logger.LogInformation("Creating test suite user {User}", suiteUser.UserName);

                _ = await userManager.CreateAsync(suiteUser, user.Password);

                // the following line was moved from outer scope to here to ensure role is added only when user is created,
                // otherwise we would revert a possible role removal executed via settings dialog
                foreach (var moduleId in moduleIds)
                {
                    logger.LogDebug("Adding {Access} access for {User} to module {ModuleId}", suiteUser.UserName, user.AccessLevel, moduleId);

                    _ = await userManager.AddToRoleAsync(suiteUser, GetRoleNameByConvention(moduleId, user.AccessLevel));
                }
                continue;
            }

            // Ensure test admin users always have "System Administrator"-role
            if (user.AccessLevel == AccessLevel.Full)
            {
                logger.LogDebug("Adding full access for {User} to module {ModuleId}", suiteUser.UserName, Constants.SystemModuleId);

                _ = await userManager.AddToRoleAsync(suiteUser, GetSystemAdministratorRoleName());
            }
        }
    }

    private static async Task SeedAdministrator(this IServiceProvider services, UserManagementOptions options, ILogger logger)
    {
        if (string.IsNullOrWhiteSpace(options.AdministratorName))
            return;

        var userManager = services.GetRequiredService<UserManager<SuiteUser>>();

        var moduleIds = services.GetRequiredService<IModuleHost>()
            .GetModules()
            .Select(m => m.ModuleKey.ModuleId)
            .Except([Module.Constants.BlazorServerModuleId])
            .Union([Constants.SystemModuleId]);

        var suiteUser = await userManager.FindByNameAsync(options.AdministratorName);
        if (suiteUser is null)
        {
            if (userManager.Users.Any()) // Only seed, if the database is untouched
                return;

            logger.LogInformation("Creatinginitial suite system user");

            suiteUser = new SuiteUser
            {
                UserName = options.AdministratorName,
                EmailConfirmed = true,
                PhoneNumberConfirmed = true,
                PasswordExpirationDate = DateTimeOffset.MinValue // Create with an expired PW to force the user to set one
            };

            _ = await userManager.CreateAsync(suiteUser, options.InitialAdministratorPassword);
        }

        // Administrator User shall always have full access to all Modules
        foreach (var moduleId in moduleIds)
        {
            var role = GetRoleNameByConvention(moduleId, AccessLevel.Full);

            logger.LogDebug("Adding full access for {User} to module {ModuleId}", suiteUser.UserName, moduleId);

            _ = await userManager.AddToRoleAsync(suiteUser, role);
        }
    }

    internal static SuiteUser CreateSuiteUserWithDefaults(SeedUser seedUser)
        => new()
        {
            UserName = seedUser.UserName,
            Email = seedUser.Email,
            FirstName = seedUser.FirstName,
            LastName = seedUser.LastName,
            Title = seedUser.Title,
            Department = "UI & UX Design",
            Occupation = "UI Designer",
            Street = "Am Eichwald",
            ZipCode = "08527",
            City = "Zwickau",
            Country = "Germany",
            PhoneNumber = "+49 12345 67890",
            Mobile = "01551 78 89 123",
            Language = null,
            TimeZone = null,
            EmailConfirmed = true,
            PhoneNumberConfirmed = true
        };

    internal static string GetSystemAdministratorRoleName()
        => GetRoleNameByConvention(Constants.SystemModuleId, AccessLevel.Full);

    internal static string GetRoleNameByConvention(string moduleId, AccessLevel accessLevel)
        => accessLevel switch
        {
            AccessLevel.Partial => $"{moduleId} User",
            AccessLevel.Full => $"{moduleId} Admin",
            _ => throw new ArgumentOutOfRangeException(nameof(accessLevel), accessLevel, null)
        };

    internal class SeedUser
    {
        public required string UserName { get; init; }
        public required string FirstName { get; init; }
        public required string LastName { get; init; }
        public string? Title { get; init; }
        public required string Email { get; init; }
        public required string Password { get; init; }
        public required AccessLevel AccessLevel { get; init; }
    }
}
