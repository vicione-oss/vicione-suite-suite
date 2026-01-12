using Core.OS.Modules.Services;
using Core.OS.UserManagement.Configuration;
using Core.Shared.UserManagement.Contracts;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using Sdk.Authorization;

namespace Core.OS.UserManagement.Extensions;

public static partial class SeedingExtensions
{
    internal const string AdminRoleName = "Administrator";
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
        var roleManager = services.GetRequiredService<RoleManager<SuiteRole>>();
        var moduleAuthorizationClaimParser = services.GetRequiredService<IModuleAuthorizationClaimParser>();
        var features = services.GetServices<IModuleFeature>();
        var adminRole = await roleManager.FindByNameAsync(AdminRoleName).ConfigureAwait(false);

        if (adminRole is null)
        {
            // Delete roles created before there was a sysadmin - can be removed after a while
            foreach (var existingRole in roleManager.Roles)
                await roleManager.DeleteAsync(existingRole).ConfigureAwait(false);

            adminRole = new SuiteRole(AdminRoleName)
            {
                Description = "The system administrator role with full access to all features.",
                Managed = true
            };
            var resultRole = await roleManager.CreateAsync(adminRole).ConfigureAwait(false);
            if(!resultRole.Succeeded)
                throw new InvalidOperationException($"Failed to create role '{AdminRoleName}': " + resultRole.Errors.First().Description);
        }

        foreach (var feature in features)
        {
            var assignedClaims = await roleManager.GetClaimsAsync(adminRole);
            if (!assignedClaims.Any(claim
                    => moduleAuthorizationClaimParser.TryParse(claim, out var claimValue)
                       && claimValue.ModuleId == feature.ModuleId
                       && claimValue.AccessLevel == AccessLevel.Full
                       && claimValue.FeatureName == feature.Name))
            {
                LogSeedRolesCreateClaimModuleIdNamePath(logger, feature.ModuleId, feature.Name, feature.Path);
                var newClaim = ModuleAuthorizationClaimFactory.CreateClaim(feature.ModuleId, AccessLevel.Full, feature.Name);
                var resultAddClaim = await roleManager.AddClaimAsync(adminRole, newClaim);
                if(!resultAddClaim.Succeeded)
                    throw new InvalidOperationException($"Failed to add claim {newClaim} to role '{AdminRoleName}': " + resultAddClaim.Errors.First().Description);
            }
        }
    }

    private static async Task SeedTestUsers(this IServiceProvider services, UserManagementOptions options, ILogger logger)
    {
        if (!options.SeedTestUsers)
            return;

        var userManager = services.GetRequiredService<UserManager<SuiteUser>>();

        foreach (var user in Users)
        {
            var suiteUser = await userManager.FindByNameAsync(user.UserName);
            if (suiteUser is null)
            {
                suiteUser = CreateSuiteUserWithDefaults(user);

                LogCreatingTestSuiteUser(logger, suiteUser.UserName);

                var resultUser = await userManager.CreateAsync(suiteUser, user.Password);
                if(!resultUser.Succeeded)
                    throw new InvalidOperationException($"Failed to create user {suiteUser.UserName}: {resultUser.Errors.First().Description}");
            }

            // Ensure test admin users always have "System Administrator"-role
            if (user.AccessLevel != AccessLevel.Full)
                continue;

            LogAddingFullAccessForUserToModule(logger, suiteUser.UserName, Shared.Constants.SystemModuleId);

            if (await userManager.IsInRoleAsync(suiteUser, AdminRoleName))
                continue;

            var resultAddToRole = await userManager.AddToRoleAsync(suiteUser, AdminRoleName);
            if (!resultAddToRole.Succeeded)
                throw new InvalidOperationException($"Failed to add {suiteUser.UserName} to {AdminRoleName}: {resultAddToRole.Errors.First().Description}");
        }
    }

    private static async Task SeedAdministrator(this IServiceProvider services, UserManagementOptions options, ILogger logger)
    {
        if (string.IsNullOrWhiteSpace(options.AdministratorName)
            || string.IsNullOrWhiteSpace(options.AdministratorEmail))
            return;

        var userManager = services.GetRequiredService<UserManager<SuiteUser>>();

        var suiteUser = await userManager.FindByNameAsync(options.AdministratorName);
        if (suiteUser is null)
        {
            if (userManager.Users.Any()) // Only seed, if the database is untouched
                return;

            LogCreatingInitialSuiteSystemUser(logger);

            suiteUser = new SuiteUser
            {
                UserName = options.AdministratorName,
                EmailConfirmed = true,
                Email = options.AdministratorEmail,
                PhoneNumberConfirmed = true,
                PasswordExpirationDate = DateTimeOffset.MinValue // Create with an expired PW to force the user to set one
            };

            var resultUser = await userManager.CreateAsync(suiteUser, options.InitialAdministratorPassword);
            if(!resultUser.Succeeded)
                throw new InvalidOperationException("Failed to create initial system user: " + resultUser.Errors.First().Description);
        }

        // Administrator User shall always have full access to all Modules
        if (await userManager.IsInRoleAsync(suiteUser, AdminRoleName))
            return;

        LogAddingFullAccessForUserToRole(logger, suiteUser.UserName, AdminRoleName);
        var resultAddToRole = await userManager.AddToRoleAsync(suiteUser, AdminRoleName);
        if (!resultAddToRole.Succeeded)
            throw new InvalidOperationException(
                $"Failed to add {suiteUser.UserName} to {AdminRoleName}: {resultAddToRole.Errors.First().Description}");
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

    [LoggerMessage(LogLevel.Debug, "SeedRoles -> Create claim ModuleId = {ModuleId}, Name = {Name}, Path = {Path}")]
    static partial void LogSeedRolesCreateClaimModuleIdNamePath(this ILogger logger, string ModuleId, string Name, IEnumerable<string> Path);

    [LoggerMessage(LogLevel.Information, "Creating test suite user {User}")]
    static partial void LogCreatingTestSuiteUser(this ILogger logger, string? User);

    [LoggerMessage(LogLevel.Debug, "Adding full access for {User} to module {ModuleId}")]
    static partial void LogAddingFullAccessForUserToModule(this ILogger logger, string? User, string ModuleId);

    [LoggerMessage(LogLevel.Information, "Creating initial suite system user")]
    static partial void LogCreatingInitialSuiteSystemUser(this ILogger logger);

    [LoggerMessage(LogLevel.Debug, "Adding full access for {User} to {RoleName} role")]
    static partial void LogAddingFullAccessForUserToRole(this ILogger logger, string? User, string RoleName);
}
