using Core.Shared.UserManagement.Contracts;
using Riok.Mapperly.Abstractions;
using Sdk.UserManagement.Contracts;

namespace Core.Shared.UserManagement.Extensions;

[Mapper]
public static partial class RoleExtensions
{
    [MapperIgnoreSource(nameof(SuiteRole.Id))]
    [MapperIgnoreSource(nameof(SuiteRole.NormalizedName))]
    [MapperIgnoreSource(nameof(SuiteRole.ConcurrencyStamp))]
    [MapperIgnoreTarget(nameof(Role.Claims))]
    public static partial Role Map(SuiteRole role);

    private static string NullableStringToString(string? value) => value ?? string.Empty;
    private static bool NullableBooleanToBoolean(bool? value) => value ?? false;
}

public static partial class RoleExtensions
{
    public static Role ToRole(this SuiteRole suiteRole, IEnumerable<UserManagementClaim> claims)
    {
        var role = Map(suiteRole);

        role.Claims = [.. claims];

        return role;
    }
}
