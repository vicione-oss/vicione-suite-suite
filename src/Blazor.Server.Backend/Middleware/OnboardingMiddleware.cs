using Blazor.Shared;
using Core.Shared.Instance.Services;
using Core.Shared.Security;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.FeatureManagement;
using Sdk.Instance;

namespace Blazor.Server.Backend.Middleware;

internal sealed class OnboardingMiddleware(RequestDelegate next)
{
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
        await next(context);
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
        var cspReport = new PathString(CspViolationReporting.Route);

        if (context.Request.Path.StartsWithSegments(framework, StringComparison.Ordinal)
            || context.Request.Path.StartsWithSegments(blazor, StringComparison.Ordinal)
            || context.Request.Path.StartsWithSegments(devTools, StringComparison.Ordinal)
            || context.Request.Path.StartsWithSegments(cookieUpdate, StringComparison.Ordinal)
            || context.Request.Path.StartsWithSegments(cspReport, StringComparison.Ordinal))
        {
            return false;
        }

        var isFeatureEnabled = await IsOnboardingFeatureEnabled(context);
        if (!isFeatureEnabled)
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

    private static async Task<bool> IsOnboardingFeatureEnabled(HttpContext context)
    {
        var featureManager = context.RequestServices.GetRequiredService<IFeatureManager>();
        return await featureManager.IsEnabledAsync(Core.Shared.Features.Constants.FirstRunWizardFeatureName);
    }
}
