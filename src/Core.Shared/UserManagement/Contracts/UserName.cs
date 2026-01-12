namespace Core.Shared.UserManagement.Contracts;

[StronglyTypedId(backingType: StronglyTypedIdBackingType.String, jsonConverter: StronglyTypedIdJsonConverter.SystemTextJson)]
public readonly partial struct UserName
{
    public UserName()
        => Value = string.Empty; // assign empty to avoid possible null-reference exception in generated Equals()

    public static bool operator <(UserName left, UserName right)
        => left.CompareTo(right) < 0;

    public static bool operator <=(UserName left, UserName right)
        => left.CompareTo(right) <= 0;

    public static bool operator >(UserName left, UserName right)
        => left.CompareTo(right) > 0;

    public static bool operator >=(UserName left, UserName right)
        => left.CompareTo(right) >= 0;
}
