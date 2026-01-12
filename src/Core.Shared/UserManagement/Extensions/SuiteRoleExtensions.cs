using Core.Shared.UserManagement.Contracts;
using Riok.Mapperly.Abstractions;
using Sdk.UserManagement.Contracts;

namespace Core.Shared.UserManagement.Extensions;

[Mapper]
public static partial class SuiteRoleExtensions
{
    [MapperIgnoreTarget(nameof(SuiteRole.ConcurrencyStamp))]
    [MapperIgnoreTarget(nameof(SuiteRole.Id))]
    [MapperIgnoreTarget(nameof(SuiteRole.NormalizedName))]
    [MapperIgnoreSource(nameof(Role.Claims))]
    public static partial SuiteRole Map(Role role);
}

public static partial class SuiteRoleExtensions
{
    public static SuiteRole ToSuiteRole(this Role role)
        => Map(role);
}
