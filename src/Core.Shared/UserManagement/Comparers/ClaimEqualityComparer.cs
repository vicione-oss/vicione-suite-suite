using System.Diagnostics.CodeAnalysis;
using System.Security.Claims;

namespace Core.Shared.UserManagement.Comparers;

public sealed class ClaimEqualityComparer : IEqualityComparer<Claim>
{
    public bool Equals(Claim? x, Claim? y)
        => string.Equals(x?.Type, y?.Type, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(x?.Value, y?.Value, StringComparison.OrdinalIgnoreCase);

    public int GetHashCode([DisallowNull] Claim obj)
        => HashCode.Combine(obj.Type, obj.Value);
}
