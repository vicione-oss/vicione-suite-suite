using Core.OS.Mail;
using Core.OS.UserManagement.Configuration;

namespace Core.OS.Extensions;

public static class OptionsExtensions
{
    extension(ConfigurationManager config)
    {
        public UserManagementOptions GetUserManagementOptions()
            => config
                .GetSection(UserManagementOptions.ConfigSection)
                .Get<UserManagementOptions>() ?? new();

        public SmtpMailOptions? GetSmtpOptions()
            => config
                .GetSection(SmtpMailOptions.ConfigSection)
                .Get<SmtpMailOptions>();
    }
}
