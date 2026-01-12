using Core.OS.Mail;
using Core.OS.UserManagement.Configuration;

namespace Core.OS.Extensions;

public static class OptionsExtensions
{
    public static void AddSuiteOptions<T>(this IServiceCollection services, string sectionName) where T : class
        => services.AddOptions<T>()
            .BindConfiguration(sectionName)
            .ValidateDataAnnotations();

    public static UserManagementOptions GetUserManagementOptions(this ConfigurationManager config)
        => config
            .GetSection(UserManagementOptions.ConfigSection)
            .Get<UserManagementOptions>() ?? new();

    public static SmtpMailOptions? GetSmtpOptions(this ConfigurationManager config)
        => config
            .GetSection(SmtpMailOptions.ConfigSection)
            .Get<SmtpMailOptions>();
}
