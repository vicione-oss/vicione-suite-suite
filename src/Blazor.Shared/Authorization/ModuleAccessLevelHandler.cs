using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Sdk.Authorization;
using Sdk.Modules;

namespace Blazor.Shared.Authorization;

public sealed class ModuleAccessLevelHandler(IModuleAuthorizationClaimParser moduleAuthorizationClaimParser)
    : AuthorizationHandler<AccessLevelAuthorizationRequirement>
{
    protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, AccessLevelAuthorizationRequirement requirement)
    {
        if (EvaluateUserAccess(context.User, requirement.ModuleId, requirement.FeatureName, requiredAccessLevel: requirement.MinimumAccessLevel))
            context.Succeed(requirement);
        else
            context.Fail();

        return Task.CompletedTask;
    }

    private bool EvaluateUserAccess(ClaimsPrincipal user, string moduleId, string? featureName, AccessLevel requiredAccessLevel)
        => user.HasClaim(claim =>
        {
            if (!moduleAuthorizationClaimParser.TryParse(claim, out var claimValue))
                return false;

            if (!moduleId.Equals(claimValue.ModuleId, StringComparison.Ordinal))
                return false;

            if (!(featureName ?? ModuleIdResolver.GetModuleName(moduleId)).Equals(claimValue.FeatureName, StringComparison.Ordinal))
                return false;

            return HasAccess(claimValue.AccessLevel, requiredAccessLevel);
        });

    private static bool HasAccess(AccessLevel assignedAccessLevel, AccessLevel requiredAccessLevel)
        => requiredAccessLevel switch
        {
            AccessLevel.Partial => assignedAccessLevel is AccessLevel.Partial or AccessLevel.Full,
            AccessLevel.Full => assignedAccessLevel == AccessLevel.Full,
            _ => false
        };
}
