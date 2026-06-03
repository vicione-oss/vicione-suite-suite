using System.Globalization;
using Core.Shared.UserManagement.Contracts;

namespace Blazor.Shared.Profile.Extensions;

internal static class IUserProfileExtensions
{
    extension(UserProfile userProfile)
    {
        public string GetFullName()
            => $"{userProfile.Firstname} {userProfile.Lastname}";

        public bool HasFullName()
            => !string.IsNullOrWhiteSpace(userProfile.Firstname) || !string.IsNullOrWhiteSpace(userProfile.Lastname);

        public string GetInitialLetters()
        {
            var initials = string.Concat(
                new[] { userProfile.Firstname ?? "", userProfile.Lastname ?? "" }
                    .Select(n => n.TrimStart())
                    .Where(n => n.Length > 0)
                    .Select(n => n[0]));

            if (string.IsNullOrEmpty(initials))
                initials = userProfile.UserName.Value.TrimStart().FirstOrDefault().ToString();

            return initials.ToUpper(CultureInfo.CurrentUICulture);
        }
    }
}
