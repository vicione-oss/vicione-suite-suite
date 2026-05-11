using Core.OS.Modules.Services;
using Core.OS.UserManagement.Extensions;
using Core.OS.UserManagement.Templates;
using Core.Shared.Mail;
using Core.Shared.Security;
using Core.Shared.UserManagement.Contracts;
using Fluid;
using MassTransit;
using Microsoft.AspNetCore.Identity;

namespace Core.OS.UserManagement.Consumers.Mail;

public sealed partial class SendResetPasswordLinkConsumer(
    IMailSender mailSender,
    UserManager<SuiteUser> userManager,
    FluidTemplateRenderer templateRenderer,
    UserManagementTemplates userManagementTemplates,
    ILogger<SendResetPasswordLinkConsumer> logger) : IConsumer<SendResetPasswordLink>
{
    public async Task Consume(ConsumeContext<SendResetPasswordLink> context)
    {
        var correlationId = context.Message.CorrelationId;
        var userId = context.Message.UserId;

        LogConsume(logger, correlationId, userId);

        try
        {
            var user = await userManager.GetUserById(userId);

            var message = await CreateMessage(context, user);

            await mailSender.SendMail(message);

            LogMailSent(logger, correlationId, userId);
        }
        catch (Exception e)
        {
            LogUnexpectedError(logger, e, correlationId, userId);
        }
    }

    private async Task<Message> CreateMessage(ConsumeContext<SendResetPasswordLink> context, SuiteUser user)
    {
        var emailBody = await RenderEmailBody(context, user);

        var message = new Message
        {
            Subject = "Reset password",
            Body = emailBody,
            RecipientAddress = user.Email!
        };
        return message;
    }

    private async Task<string> RenderEmailBody(ConsumeContext<SendResetPasswordLink> context, SuiteUser user)
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
                        { "Title", "Reset password" }
                    });
            },
            context.CancellationToken);
        return emailBody;
    }

    [LoggerMessage(Level = LogLevel.Debug, Message = "Sending reset email to '{Name}' correlated by {CorrelationId}.")]
    private static partial void LogConsume(ILogger<SendResetPasswordLinkConsumer> logger, Guid correlationId, string name);

    [LoggerMessage(Level = LogLevel.Information, Message = "Reset email to '{Name}' correlated by {CorrelationId} was sent.")]
    private static partial void LogMailSent(ILogger<SendResetPasswordLinkConsumer> logger, Guid correlationId, string name);

    [LoggerMessage(Level = LogLevel.Error, Message = "Unexpected error occured on sending reset email to '{Name}' correlated by '{CorrelationId}'.")]
    private static partial void LogUnexpectedError(ILogger<SendResetPasswordLinkConsumer> logger, Exception ex, Guid correlationId, string name);
}
