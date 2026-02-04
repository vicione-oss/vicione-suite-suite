using Core.Shared.UserManagement.Contracts;
using Sdk.Authorization;

namespace Core.Shared.UserManagement.Extensions;

public static class UserProfileExtensions
{
    extension(UserProfile userProfile)
    {
        public bool IsAuthorizationChanged(UserProfile userProfileBefore)
        {
            if (userProfile.Roles.Except(userProfileBefore.Roles).Any())
                return true;

            if (userProfileBefore.Roles.Except(userProfile.Roles).Any())
                return true;

            var moduleAuthorizationClaims = userProfile.Claims.Where(claim => claim.Type == SuiteClaimTypes.ModuleAuthorization).ToList();
            var moduleAuthorizationClaimsBefore = userProfileBefore.Claims.Where(claim => claim.Type == SuiteClaimTypes.ModuleAuthorization).ToList();

            if (moduleAuthorizationClaims.Except(moduleAuthorizationClaimsBefore).Any())
                return true;

            if (moduleAuthorizationClaimsBefore.Except(moduleAuthorizationClaims).Any())
                return true;

            return false;
        }

        public bool IsLanguageChanged(UserProfile userProfileBefore)
            => userProfile.Language != userProfileBefore.Language;
    }
}
