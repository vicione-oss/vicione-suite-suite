namespace Core.Shared.UserManagement.Contracts;

[StronglyTypedId(backingType: StronglyTypedIdBackingType.String, jsonConverter: StronglyTypedIdJsonConverter.SystemTextJson)]
public readonly partial struct Role
{
    public Role()
        => Value = string.Empty; // assign empty to avoid possible null-reference exception in generated Equals()

    public static bool operator <(Role left, Role right)
        => left.CompareTo(right) < 0;

    public static bool operator <=(Role left, Role right)
        => left.CompareTo(right) <= 0;

    public static bool operator >(Role left, Role right)
        => left.CompareTo(right) > 0;

    public static bool operator >=(Role left, Role right)
        => left.CompareTo(right) >= 0;
}
