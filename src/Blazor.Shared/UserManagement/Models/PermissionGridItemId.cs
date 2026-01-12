namespace Blazor.Shared.UserManagement.ControlPanels.Models;

[StronglyTypedId(backingType: StronglyTypedIdBackingType.Guid, jsonConverter: StronglyTypedIdJsonConverter.SystemTextJson)]
public readonly partial struct PermissionGridItemId
{
    public PermissionGridItemId()
        => Value = Guid.Empty; // assign empty to avoid possible null-reference exception in generated Equals()

    public static bool operator <(PermissionGridItemId left, PermissionGridItemId right)
        => left.CompareTo(right) < 0;

    public static bool operator <=(PermissionGridItemId left, PermissionGridItemId right)
        => left.CompareTo(right) <= 0;

    public static bool operator >(PermissionGridItemId left, PermissionGridItemId right)
        => left.CompareTo(right) > 0;

    public static bool operator >=(PermissionGridItemId left, PermissionGridItemId right)
        => left.CompareTo(right) >= 0;
}
