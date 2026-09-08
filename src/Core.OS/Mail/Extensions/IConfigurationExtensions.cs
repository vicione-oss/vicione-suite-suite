namespace Core.OS.Mail.Extensions;

internal static class IConfigurationExtensions
{
    internal static SmtpMailOptions? GetSmtpOptions(this IConfiguration config)
        => config.GetSection(SmtpMailOptions.ConfigSection).Get<SmtpMailOptions>();
}
