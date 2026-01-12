using Blazor.Shared.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Infrastructure;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;
using Sdk.Authorization;
using Xunit;

namespace Blazor.Shared.Tests.Authorization;

public class AccessLevelPolicyProviderFacts
{
    private static readonly ILogger<AccessLevelPolicyProvider> Logger = Substitute.For<ILogger<AccessLevelPolicyProvider>>();

    public class GetDefaultPolicyAsync
    {
        [Fact]
        public async Task Acceptance()
        {
            var provider = new AccessLevelPolicyProvider(Options.Create(new AuthorizationOptions()), Logger);

            var defaultPolicy = await provider.GetDefaultPolicyAsync();

            Assert.IsType<DenyAnonymousAuthorizationRequirement>(defaultPolicy.Requirements[0]);
        }
    }

    public class GetFallbackPolicyAsync
    {
        [Fact]
        public async Task Acceptance()
        {
            var provider = new AccessLevelPolicyProvider(Options.Create(new AuthorizationOptions()), Logger);

            var result = await provider.GetFallbackPolicyAsync();
            Assert.Null(result);
        }
    }

    public class GetPolicyAsync
    {
        private const string moduleId = "moduleId";
        private const string feature = "feature";
        private const AccessLevel AccessLevel = Sdk.Authorization.AccessLevel.Partial;

        [Fact]
        public async Task AcceptanceWithoutFeature()
        {
            var provider = new AccessLevelPolicyProvider(Options.Create(new AuthorizationOptions()), Logger);

            var policy = await provider.GetPolicyAsync($"{Sdk.Constants.AuthorizationPolicyPrefix}_{moduleId}_{AccessLevel}");

            Assert.Equal(moduleId, ((AccessLevelAuthorizationRequirement)policy!.Requirements[0]).ModuleId);
            Assert.Equal(AccessLevel, ((AccessLevelAuthorizationRequirement)policy.Requirements[0]).MinimumAccessLevel);
        }

        [Fact]
        public async Task AcceptanceWithFeature()
        {
            var provider = new AccessLevelPolicyProvider(Options.Create(new AuthorizationOptions()), Logger);

            var policy = await provider.GetPolicyAsync($"{Sdk.Constants.AuthorizationPolicyPrefix}_{moduleId}_{AccessLevel}_{feature}");

            Assert.Equal(moduleId, ((AccessLevelAuthorizationRequirement)policy!.Requirements[0]).ModuleId);
            Assert.Equal(AccessLevel, ((AccessLevelAuthorizationRequirement)policy.Requirements[0]).MinimumAccessLevel);
            Assert.Equal(feature, ((AccessLevelAuthorizationRequirement)policy.Requirements[0]).FeatureName);
        }

        [Fact]
        public async Task WrongPrefix()
        {
            var provider = new AccessLevelPolicyProvider(Options.Create(new AuthorizationOptions()), Logger);

            var policy = await provider.GetPolicyAsync($"notfeature_{moduleId}_{AccessLevel}_{feature}");

            Assert.Null(policy);
        }
    }
}
