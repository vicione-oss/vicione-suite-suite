namespace Core.Shared.UserManagement.Configuration;

/// <summary>
/// Shared by the settings panel and the store consumer, so the bus cannot store an authority the panel rejects.
/// </summary>
public static class ExternalIdProviderValidation
{
    /// <summary>
    /// Absolute <c>https</c> only: the handler sets <c>RequireHttpsMetadata = true</c>, so anything else
    /// would fail only at sign-in.
    /// </summary>
    public static bool IsValidAuthority(string? authority)
        => Uri.TryCreate(authority, UriKind.Absolute, out var uri)
            && string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.Ordinal);
}
