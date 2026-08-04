using System.Security.Claims;
using Blazor.Shared.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Sdk.Authorization;
using Sdk.Authorization.Extensions;

namespace Blazor.Shared.Tests.Authorization;

public class ModuleAccessLevelHandlerFacts
{
    public sealed class HandleRequirementAsync : ModuleAccessLevelHandlerFacts
    {
        private readonly ModuleAccessLevelHandler _moduleAccessLevelHandler;

        public HandleRequirementAsync()
        {
            var services = new ServiceCollection()
                .AddLogging()
                .AddSdkAuthorization()
                .AddSingleton<ModuleAccessLevelHandler>();

            using var serviceProvider = services.BuildServiceProvider();

            _moduleAccessLevelHandler = serviceProvider.GetRequiredService<ModuleAccessLevelHandler>();
        }

        [Theory]
        [InlineData(AccessLevel.Partial)]
        [InlineData(AccessLevel.Full)]
        public async Task Should_succeed_for_different_access_levels_with_full_user(AccessLevel accessLevel)
        {
            // Arrange
            var context = Initialize(accessLevel, AccessLevel.Full);

            // Act
            await _moduleAccessLevelHandler.HandleAsync(context);

            // Assert
            context.HasFailed.Should().BeFalse();
            context.HasSucceeded.Should().BeTrue();
        }

        [Fact]
        public async Task Should_succeed_for_partial_access_level_with_matching_feature()
        {
            // Arrange
            var context = Initialize(AccessLevel.Partial, AccessLevel.Partial, "Feature");

            // Act
            await _moduleAccessLevelHandler.HandleAsync(context);

            // Assert
            context.HasFailed.Should().BeFalse();
            context.HasSucceeded.Should().BeTrue();
        }

        [Fact]
        public async Task Should_succeed_for_different_access_levels_with_partial_user()
        {
            // Arrange
            var context = Initialize(AccessLevel.Partial, AccessLevel.Partial);

            // Act
            await _moduleAccessLevelHandler.HandleAsync(context);

            // Assert
            context.HasFailed.Should().BeFalse();
            context.HasSucceeded.Should().BeTrue();
        }

        [Fact]
        public async Task Should_fail_when_full_access_level_required_with_partial_user()
        {
            // Arrange
            var context = Initialize(AccessLevel.Full, AccessLevel.Partial);

            // Act
            await _moduleAccessLevelHandler.HandleAsync(context);

            // Assert
            context.HasFailed.Should().BeTrue();
            context.HasSucceeded.Should().BeFalse();
        }

        private static AuthorizationHandlerContext Initialize(AccessLevel requiredAccessLevel, AccessLevel userAccessLevel, string? feature = null)
        {
            const string moduleId = "ModuleId";

            var authorizationRequirements = new List<IAuthorizationRequirement>
            {
                new AccessLevelAuthorizationRequirement(moduleId, requiredAccessLevel)
                    { FeatureName = feature }
            };

            var claim = ModuleAuthorizationClaimFactory.CreateClaim(moduleId, userAccessLevel, feature ?? moduleId);

            var user = new ClaimsPrincipal(new ClaimsIdentity([claim]));

            return new AuthorizationHandlerContext(authorizationRequirements, user, null);
        }
    }
}
