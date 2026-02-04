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
            var initialLetters = $"{userProfile.Firstname?.FirstOrDefault(' ')}{userProfile.Lastname?.FirstOrDefault(' ')}";

            if (string.IsNullOrWhiteSpace(initialLetters))
                initialLetters = userProfile.UserName.Value.FirstOrDefault().ToString();

            return initialLetters.ToUpper(CultureInfo.CurrentUICulture);
        }
    }
}
