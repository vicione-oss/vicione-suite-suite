using Core.OS.Extensions;
using Core.OS.Mail.MailKit;
using Core.Shared.Mail;
using Microsoft.AspNetCore.Identity.UI.Services;

namespace Core.OS.Mail.Extensions;

public static class IServiceCollectionExtensions
{
    public static IServiceCollection AddMailing(this IServiceCollection services)
    {
        // Every field is `required` but none is `[Required]`: the binder does not enforce `required`, and adding
        // the attribute would stop every instance that does not configure mail at all. Checked where it is used
        // (MailkitMailSender reports an unusable configuration through IMailSenderStatus) instead.
        services.AddUnvalidatedSuiteOptions<SmtpMailOptions>(SmtpMailOptions.ConfigSection,
            "Mail is optional; an unconfigured Smtp section must not stop the instance.");
        services.AddTransient<IMailClientConfigurator, StrictMailClientConfigurator>();

        services.AddTransient<IMailSender, MailkitMailSender>();
        services.AddTransient<IEmailSender, MailkitMailSender>();
        services.AddTransient<IMailSenderStatus, MailkitMailSender>();

        return services;
    }
}
