using Core.Shared.UserManagement.Contracts;
using Riok.Mapperly.Abstractions;

namespace Core.Shared.UserManagement.Mappers;

[Mapper(UseDeepCloning = true)]
public partial class UserProfileMapper
{
    public partial UserProfile Map(UserProfile source);

    private static UserName Map(UserName source)
        => source;

    private static SuiteRole Map(SuiteRole source)
        => source;
}
