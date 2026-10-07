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
        var needsRedirect = await NeedsOnboaringRedirect(context);
        if (needsRedirect)
        {
            context.Response.Redirect(Shared.Onboarding.Constants.Route);
            return;
        }

        await next(context);
    }

    private static async Task<bool> NeedsOnboaringRedirect(HttpContext context)
    {
        // Onboarding needs a signed-in user.
        if (context.User.Identity is null || !context.User.Identity.IsAuthenticated)
        {
            return false;
        }

        // Internal framework calls are left alone.
        var framework = new PathString("/_framework");
        var blazor = new PathString("/_blazor");
        var devTools = new PathString("/.well-known");
        var cookieUpdate = new PathString($"/{Constants.UpdateLanguageCookieRoute}");
        var cspReport = new PathString(CspViolationReporting.Route);

        if (context.Request.Path.StartsWithSegments(framework, StringComparison.OrdinalIgnoreCase)
            || context.Request.Path.StartsWithSegments(blazor, StringComparison.OrdinalIgnoreCase)
            || context.Request.Path.StartsWithSegments(devTools, StringComparison.OrdinalIgnoreCase)
            || context.Request.Path.StartsWithSegments(cookieUpdate, StringComparison.OrdinalIgnoreCase)
            || context.Request.Path.StartsWithSegments(cspReport, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var isFeatureEnabled = await IsOnboardingFeatureEnabled(context);
        if (!isFeatureEnabled)
        {
            return false;
        }

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
            // A cancelled request needs no redirect.
            return false;
        }
    }

    private static async Task<bool> IsOnboardingFeatureEnabled(HttpContext context)
    {
        var featureManager = context.RequestServices.GetRequiredService<IFeatureManager>();
        return await featureManager.IsEnabledAsync(Core.Shared.Features.Constants.FirstRunWizardFeatureName);
    }
}
