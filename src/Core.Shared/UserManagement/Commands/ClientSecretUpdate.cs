namespace Core.Shared.UserManagement.Commands;

/// <summary>
/// What a <see cref="SetExternalIdProvider"/> does to the stored client secret; the browser never has
/// the secret, so an empty field cannot tell "keep" from "remove".
/// </summary>
public sealed record ClientSecretUpdate
{
    public static ClientSecretUpdate Keep { get; } = new() { Kind = ClientSecretUpdateKind.Keep };

    public static ClientSecretUpdate Clear { get; } = new() { Kind = ClientSecretUpdateKind.Clear };

    public required ClientSecretUpdateKind Kind { get; init; }

    public string? Value { get; init; }

    public static ClientSecretUpdate Set(string value)
        => new() { Kind = ClientSecretUpdateKind.Set, Value = value };
}
