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

public sealed class SendResetPasswordLinkConsumer(
    IMailSender mailSender,
    UserManager<SuiteUser> userManager,
    FluidTemplateRenderer templateRenderer,
    UserManagementTemplates userManagementTemplates,
    ILogger<SendResetPasswordLinkConsumer> logger) : IConsumer<SendResetPasswordLink>
{
    public async Task Consume(ConsumeContext<SendResetPasswordLink> context)
    {
        try
        {
            var userId = context.Message.UserId;
            var user = await userManager.GetUserById(userId);

            var message = await CreateMessage(context, user);

            await mailSender.SendMail(message);
        }
        catch (Exception e)
        {
            logger.LogError(e, "Failed to send reset password link");
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
}
