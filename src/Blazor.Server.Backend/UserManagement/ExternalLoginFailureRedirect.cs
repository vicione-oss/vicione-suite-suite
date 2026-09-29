using Blazor.Server.Backend.Extensions;
using Blazor.Shared;
using Core.Shared.Security;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Blazor.Server.Backend.UserManagement;

/// <summary>
/// Sends the browser back to the login page, with a message, when the OpenID provider ends a
/// sign-in instead of completing it.
/// </summary>
/// <remarks>
/// Such a callback never reaches <see cref="Endpoints.ExternalLogin.HandleLoginCallback"/>. Left alone,
/// <see cref="RemoteAuthenticationHandler{TOptions}"/> throws and the user sees the generic error page.
/// </remarks>
internal sealed partial class ExternalLoginFailureRedirect(ILogger<ExternalLoginFailureRedirect> logger)
    : IConfigureNamedOptions<OpenIdConnectOptions>
{
    public void Configure(OpenIdConnectOptions options) => Configure(Options.DefaultName, options);

    public void Configure(string? name, OpenIdConnectOptions options)
    {
        if (name != OpenIdConnectDefaults.AuthenticationScheme)
            return;

        options.Events.OnAccessDenied = context => SendToLoginPage(context, ExternalLoginError.SignInCancelled);
        options.Events.OnRemoteFailure = SendFailureToLoginPage;
    }

    private Task SendFailureToLoginPage(RemoteFailureContext context)
    {
        LogRemoteFailure(logger, context.Failure?.Message);

        return SendToLoginPage(context, ExternalLoginError.LoginFailed);
    }

    private static Task SendToLoginPage(
        HandleRequestContext<RemoteAuthenticationOptions> context, ExternalLoginError error)
    {
        // Without this the handler carries on and throws, whatever is written to the response.
        context.HandleResponse();

        context.HttpContext.RequestServices
            .GetRequiredService<ITempDataDictionaryFactory>()
            .AddExternalError(context.HttpContext, error);

        context.Response.Redirect(IdentityRoutes.LoginRoute);

        return Task.CompletedTask;
    }

    [LoggerMessage(LogLevel.Warning, "External sign-in failed: {Reason}")]
    private static partial void LogRemoteFailure(ILogger<ExternalLoginFailureRedirect> logger, string? reason);
}
