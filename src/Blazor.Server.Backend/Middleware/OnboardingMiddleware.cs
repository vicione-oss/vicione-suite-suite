using Blazor.Shared;
using Core.Shared.Instance.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Sdk.Instance;

namespace Blazor.Server.Backend.Middleware;

internal sealed class OnboardingMiddleware(RequestDelegate next)
{
    private readonly RequestDelegate _next = next;

    public async Task InvokeAsync(HttpContext context)
    {
        // Redirect to onboarding page if neccessary
        var needsRedirect = await NeedsOnboaringRedirect(context);
        if (needsRedirect)
        {
            context.Response.Redirect(Shared.Onboarding.Constants.Route);
            return;
        }

        // Call the next delegate/middleware in the pipeline.
        await _next(context);
    }

    private static async Task<bool> NeedsOnboaringRedirect(HttpContext context)
    {
        // User not logged -> no onboarding possible
        if (context.User.Identity is null || !context.User.Identity.IsAuthenticated)
        {
            return false;
        }

        // These are internal framework calls and we don't interfere
        var framework = new PathString("/_framework");
        var blazor = new PathString("/_blazor");
        var devTools = new PathString("/.well-known");
        var cookieUpdate = new PathString($"/{Constants.UpdateLanguageCookieRoute}");

        if (context.Request.Path.StartsWithSegments(framework, StringComparison.Ordinal)
            || context.Request.Path.StartsWithSegments(blazor, StringComparison.Ordinal)
            || context.Request.Path.StartsWithSegments(devTools, StringComparison.Ordinal)
            || context.Request.Path.StartsWithSegments(cookieUpdate, StringComparison.Ordinal))
        {
            return false;
        }

        // Check instance onboarding state
        var instanceInformation = context.RequestServices.GetRequiredService<IInstanceInformationProvider>().Local;
        var onboardingStore = context.RequestServices.GetRequiredService<IOnboardingStateStore>();

        try
        {
            var onboardingState = await onboardingStore.GetOnboardingStateAsync(instanceInformation.Id, context.RequestAborted);
            if (onboardingState.Completed)
                return false;

            if (context.Request.Path == Shared.Onboarding.Constants.Route)
                return false;

            return onboardingState.ShowWizardWhenNotCompleted;
        }
        catch (OperationCanceledException)
        {
            // Nothing to do here, return gracefully
            return false;
        }
    }
}
