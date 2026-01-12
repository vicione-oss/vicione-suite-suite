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

public sealed class SendVerifyEmailAddressLinkConsumer(
    IMailSender mailSender,
    UserManager<SuiteUser> userManager,
    FluidTemplateRenderer templateRenderer,
    UserManagementTemplates userManagementTemplates,
    ILogger<SendVerifyEmailAddressLinkConsumer> logger) : IConsumer<SendVerifyEmailAddressLink>
{
    public async Task Consume(ConsumeContext<SendVerifyEmailAddressLink> context)
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
            logger.LogError(e, "Failed to send verify email address link");
        }
    }

    private async Task<Message> CreateMessage(ConsumeContext<SendVerifyEmailAddressLink> context, SuiteUser user)
    {
        var body = await RenderEmailBody(user, context);

        return new Message
        {
            RecipientAddress = user.Email!,
            Subject = "Please verify your account",
            Body = body
        };
    }

    private async Task<string> RenderEmailBody(SuiteUser user,
        ConsumeContext<SendVerifyEmailAddressLink> context)
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
                        { "Title", "Verify your email address" }
                    });
            },
            context.CancellationToken);
        return body;
    }
}
