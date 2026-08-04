using Blazor.Shared.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Infrastructure;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Sdk.Authorization;

namespace Blazor.Shared.Tests.Authorization;

public class AccessLevelPolicyProviderFacts
{
    private static readonly ILogger<AccessLevelPolicyProvider> Logger = Substitute.For<ILogger<AccessLevelPolicyProvider>>();

    public sealed class GetDefaultPolicyAsync : AccessLevelPolicyProviderFacts
    {
        [Fact]
        public async Task Should_return_deny_anonymous_requirement()
        {
            // Arrange
            var provider = new AccessLevelPolicyProvider(Options.Create(new AuthorizationOptions()), Logger);

            // Act
            var defaultPolicy = await provider.GetDefaultPolicyAsync();

            // Assert
            defaultPolicy.Requirements[0].Should().BeOfType<DenyAnonymousAuthorizationRequirement>();
        }
    }

    public sealed class GetFallbackPolicyAsync : AccessLevelPolicyProviderFacts
    {
        [Fact]
        public async Task Should_return_null()
        {
            // Arrange
            var provider = new AccessLevelPolicyProvider(Options.Create(new AuthorizationOptions()), Logger);

            // Act
            var result = await provider.GetFallbackPolicyAsync();

            // Assert
            result.Should().BeNull();
        }
    }

    public sealed class GetPolicyAsync : AccessLevelPolicyProviderFacts
    {
        private const string ModuleId = "moduleId";
        private const string Feature = "feature";
        private const AccessLevel Level = Sdk.Authorization.AccessLevel.Partial;

        [Fact]
        public async Task Should_return_policy_without_feature()
        {
            // Arrange
            var provider = new AccessLevelPolicyProvider(Options.Create(new AuthorizationOptions()), Logger);

            // Act
            var policy = await provider.GetPolicyAsync($"{Sdk.Constants.AuthorizationPolicyPrefix}_{ModuleId}_{Level}");

            // Assert
            var requirement = (AccessLevelAuthorizationRequirement)policy!.Requirements[0];
            requirement.ModuleId.Should().Be(ModuleId);
            requirement.MinimumAccessLevel.Should().Be(Level);
        }

        [Fact]
        public async Task Should_return_policy_with_feature()
        {
            // Arrange
            var provider = new AccessLevelPolicyProvider(Options.Create(new AuthorizationOptions()), Logger);

            // Act
            var policy = await provider.GetPolicyAsync($"{Sdk.Constants.AuthorizationPolicyPrefix}_{ModuleId}_{Level}_{Feature}");

            // Assert
            var requirement = (AccessLevelAuthorizationRequirement)policy!.Requirements[0];
            requirement.ModuleId.Should().Be(ModuleId);
            requirement.MinimumAccessLevel.Should().Be(Level);
            requirement.FeatureName.Should().Be(Feature);
        }

        [Fact]
        public async Task Should_return_null_for_wrong_prefix()
        {
            // Arrange
            var provider = new AccessLevelPolicyProvider(Options.Create(new AuthorizationOptions()), Logger);

            // Act
            var policy = await provider.GetPolicyAsync($"notfeature_{ModuleId}_{Level}_{Feature}");

            // Assert
            policy.Should().BeNull();
        }
    }
}
