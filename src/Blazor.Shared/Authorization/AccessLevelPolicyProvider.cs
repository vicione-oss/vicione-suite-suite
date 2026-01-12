using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Sdk.Authorization;

namespace Blazor.Shared.Authorization;

public sealed class AccessLevelPolicyProvider(IOptions<AuthorizationOptions> options, ILogger<AccessLevelPolicyProvider> logger)
    : IAuthorizationPolicyProvider
{
    private DefaultAuthorizationPolicyProvider BackupPolicyProvider { get; } = new(options);

    public Task<AuthorizationPolicy> GetDefaultPolicyAsync()
        => Task.FromResult(new AuthorizationPolicyBuilder(Core.Shared.Constants.AuthenticationSchema)
            .RequireAuthenticatedUser()
            .Build());

    public Task<AuthorizationPolicy?> GetFallbackPolicyAsync()
        => BackupPolicyProvider.GetFallbackPolicyAsync();

    public Task<AuthorizationPolicy?> GetPolicyAsync(string policyName)
    {
        if (!AccessLevelPolicyParser.TryParse(policyName, out var accessLevelAuthorizationRequirement))
        {
            logger.LogWarning("Invalid policy: {PolicyName}, expected '{AuthorizationPolicyPrefix}_<ModuleId>_<AccessLevel>?_<Feature>', executing fallback to backup policy",
                policyName, Sdk.Constants.AuthorizationPolicyPrefix);

            return BackupPolicyProvider.GetPolicyAsync(policyName);
        }

        var policy = new AuthorizationPolicyBuilder(Core.Shared.Constants.AuthenticationSchema);
        policy.AddRequirements(accessLevelAuthorizationRequirement);
        return Task.FromResult<AuthorizationPolicy?>(policy.Build());
    }
}
