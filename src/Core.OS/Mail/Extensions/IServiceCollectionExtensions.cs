using Core.OS.Extensions;
using Core.OS.Mail.MailKit;
using Core.OS.Modules.Services;
using Core.OS.UserManagement.Security;
using Core.OS.UserManagement.Templates;
using Core.Shared.Mail;
using Core.Shared.Security;
using Microsoft.AspNetCore.Identity.UI.Services;

namespace Core.OS.Mail.Extensions;

public static class IServiceCollectionExtensions
{
    public static IServiceCollection AddMailing(this IServiceCollection services)
    {
        services.AddSuiteOptions<SmtpMailOptions>(SmtpMailOptions.ConfigSection);
        services.AddTransient<IMailClientConfigurator, StrictMailClientConfigurator>();

        services.AddTransient<IMailSender, MailkitMailSender>();
        services.AddTransient<IEmailSender, MailkitMailSender>();
        services.AddTransient<IMailSenderStatus, MailkitMailSender>();
        services.AddTransient<IAccountVerification, AccountVerification>();
        services.AddTransient<ISecuritySettings, SecuritySettings>();

        services.AddTransient<FluidTemplateRenderer>();
        services.AddTransient<UserManagementTemplates>();

        return services;
    }

}
