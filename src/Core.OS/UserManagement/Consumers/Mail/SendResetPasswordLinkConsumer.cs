using System.Globalization;
using Core.OS.DbContext;
using Core.OS.Modules.Services;
using Core.OS.UserManagement.Extensions;
using Core.OS.UserManagement.Templates;
using Core.OS.UserManagement.Templates.Localization;
using Core.Shared.Mail;
using Core.Shared.Security;
using Core.Shared.UserManagement.Contracts;
using Fluid;
using MassTransit;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Core.OS.UserManagement.Consumers.Mail;

public sealed partial class SendResetPasswordLinkConsumer(
    IMailSender mailSender,
    UserManager<SuiteUser> userManager,
    FluidTemplateRenderer templateRenderer,
    UserManagementTemplates userManagementTemplates,
    IApplicationDbContext dbContext,
    ILogger<SendResetPasswordLinkConsumer> logger) : IConsumer<SendResetPasswordLink>
{
    public async Task Consume(ConsumeContext<SendResetPasswordLink> context)
    {
        var correlationId = context.Message.CorrelationId;
        var userId = context.Message.UserId;

        LogConsume(logger, correlationId, userId);

        // ADR-002: do not swallow exceptions; let MassTransit retry then dead-letter.
        // Duplicate-email risk on redelivery is acceptable — recipient uses the first valid link.
        var user = await userManager.GetUserById(userId);

        var instanceConfiguration = await dbContext.CrossInstanceConfiguration.AsNoTracking()
            .SingleOrDefaultAsync(context.CancellationToken);
        var culture = EmailCulture.Resolve(user.Language, instanceConfiguration?.CultureName);
        var message = await CreateMessage(context, user, culture);

        await mailSender.SendMail(message);

        LogMailSent(logger, correlationId, userId);
    }

    private async Task<Message> CreateMessage(ConsumeContext<SendResetPasswordLink> context, SuiteUser user,
        CultureInfo culture)
    {
        var emailBody = await RenderEmailBody(context, user, culture);

        var message = new Message
        {
            Subject = EmailTextLookup.Get(nameof(EmailTexts.ResetPasswordSubject), culture),
            Body = emailBody,
            RecipientAddress = user.Email!
        };
        return message;
    }

    private async Task<string> RenderEmailBody(ConsumeContext<SendResetPasswordLink> context, SuiteUser user,
        CultureInfo culture)
    {
        var emailBody = await templateRenderer.RenderFromTemplateFile(
            userManagementTemplates.ResetPasswordEmailTemplate,
            templateContext =>
            {
                templateContext.SetValue("User", user);
                templateContext.SetValue("Model",
                    new Dictionary<string, string>
                    {
                        { "Link", context.Message.CallbackLink },
                        { "Language", culture.TwoLetterISOLanguageName }
                    });
                templateContext.SetValue("Text", EmailTextLookup.GetAll(culture));
            },
            context.CancellationToken);
        return emailBody;
    }

    [LoggerMessage(Level = LogLevel.Debug, Message = "Sending reset email to '{Name}' correlated by {CorrelationId}.")]
    private static partial void LogConsume(ILogger<SendResetPasswordLinkConsumer> logger, Guid correlationId, string name);

    [LoggerMessage(Level = LogLevel.Information, Message = "Reset email to '{Name}' correlated by {CorrelationId} was sent.")]
    private static partial void LogMailSent(ILogger<SendResetPasswordLinkConsumer> logger, Guid correlationId, string name);
}
