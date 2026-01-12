using System.Security.Claims;
using Blazor.Shared.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Sdk.Authorization;
using Sdk.Authorization.Extensions;
using Xunit;

namespace Blazor.Shared.Tests.Authorization;

public class ModuleAccessLevelHandlerFacts
{
    public class HandleRequirementAsync
    {
        private readonly ModuleAccessLevelHandler _moduleAccessLevelHandler;

        public HandleRequirementAsync()
        {
            var services = new ServiceCollection()
                .AddLogging()
                .AddSdkAuthorization()
                .AddSingleton<ModuleAccessLevelHandler>();

            var serviceProvider = services.BuildServiceProvider();

            _moduleAccessLevelHandler = serviceProvider.GetRequiredService<ModuleAccessLevelHandler>();
        }

        [Theory]
        [InlineData(AccessLevel.Partial)]
        [InlineData(AccessLevel.Full)]
        public async Task SuccessfulDifferentAccessLevelsWithFullUser(AccessLevel accessLevel)
        {
            var context = Initialize(accessLevel, AccessLevel.Full);

            await _moduleAccessLevelHandler.HandleAsync(context);

            Assert.False(context.HasFailed);
            Assert.True(context.HasSucceeded);
        }

        [Fact]
        public async Task SuccessfulAccessLevelReadWithUserReadableAndFeature()
        {
            var context = Initialize(AccessLevel.Partial, AccessLevel.Partial, "Feature");

            await _moduleAccessLevelHandler.HandleAsync(context);

            Assert.False(context.HasFailed);
            Assert.True(context.HasSucceeded);
        }

        [Fact]
        public async Task SuccessfulDifferentAccessLevelsWithUserPartial()
        {
            var context = Initialize(AccessLevel.Partial, AccessLevel.Partial);

            await _moduleAccessLevelHandler.HandleAsync(context);

            Assert.False(context.HasFailed);
            Assert.True(context.HasSucceeded);
        }

        [Fact]
        public async Task CheckForbiddenAccessLevelFullWithUserPartial()
        {
            var context = Initialize(AccessLevel.Full, AccessLevel.Partial);

            await _moduleAccessLevelHandler.HandleAsync(context);

            Assert.True(context.HasFailed);
            Assert.False(context.HasSucceeded);
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
