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

public sealed partial class SendVerifyEmailAddressLinkConsumer(
    IMailSender mailSender,
    UserManager<SuiteUser> userManager,
    FluidTemplateRenderer templateRenderer,
    UserManagementTemplates userManagementTemplates,
    IApplicationDbContext dbContext,
    ILogger<SendVerifyEmailAddressLinkConsumer> logger) : IConsumer<SendVerifyEmailAddressLink>
{
    public async Task Consume(ConsumeContext<SendVerifyEmailAddressLink> context)
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

    private async Task<Message> CreateMessage(ConsumeContext<SendVerifyEmailAddressLink> context, SuiteUser user,
        CultureInfo culture)
    {
        var body = await RenderEmailBody(user, context, culture);

        return new Message
        {
            RecipientAddress = user.Email!,
            Subject = EmailTextLookup.Get(nameof(EmailTexts.VerifyAddressSubject), culture),
            Body = body
        };
    }

    private async Task<string> RenderEmailBody(SuiteUser user,
        ConsumeContext<SendVerifyEmailAddressLink> context, CultureInfo culture)
    {
        var body = await templateRenderer.RenderFromTemplateFile(
            userManagementTemplates.VerifyAddressEmailTemplate,
            templateContext =>
            {
                templateContext.SetValue("User",
                    new Dictionary<string, string?> { { "FirstName", user.FirstName } });
                templateContext.SetValue("Model",
                    new Dictionary<string, string>
                    {
                        { "Link", context.Message.CallbackLink },
                        { "Language", culture.TwoLetterISOLanguageName }
                    });
                templateContext.SetValue("Text", EmailTextLookup.GetAll(culture));
            },
            context.CancellationToken);
        return body;
    }

    [LoggerMessage(Level = LogLevel.Debug, Message = "Sending verification email to '{Name}' correlated by {CorrelationId}.")]
    private static partial void LogConsume(ILogger<SendVerifyEmailAddressLinkConsumer> logger, Guid correlationId, string name);

    [LoggerMessage(Level = LogLevel.Information, Message = "Verification email to '{Name}' correlated by {CorrelationId} was sent.")]
    private static partial void LogMailSent(ILogger<SendVerifyEmailAddressLinkConsumer> logger, Guid correlationId, string name);
}
