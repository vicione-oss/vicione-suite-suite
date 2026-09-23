namespace Core.Shared.Security;

public interface ISecuritySettings
{
    /// <summary>
    /// Login requires a confirmed email address.
    /// </summary>
    bool RequireAccountVerification { get; }
}
