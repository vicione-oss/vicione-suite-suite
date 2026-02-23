using Core.OS.UserManagement.Configuration;
using Core.Shared.Security;
using Microsoft.Extensions.Options;

namespace Core.OS.UserManagement.Security;

public class SecuritySettings(IOptions<UserManagementOptions> userManagementOptions) : ISecuritySettings
{
    public bool RequireAccountVerification => userManagementOptions.Value.RequireAccountVerificationToLogIn;
}
