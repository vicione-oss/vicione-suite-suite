namespace Core.Shared.Security;

public interface ISecuritySettings
{
    /// <summary>
    /// If true, login will only be allowed if the users mail was confirmed. 
    /// </summary>
    bool RequireAccountVerification { get; }
}
