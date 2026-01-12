using System.Globalization;
using Blazor.Shared.Profile.Services.TicketCenter;
using Core.Shared.UserManagement.Contracts;
using Microsoft.Extensions.DependencyInjection;

namespace Blazor.Shared.Profile.Extensions;

internal static class IUserProfileExtensions
{
    public static string GetFullName(this UserProfile userProfile)
        => $"{userProfile.Firstname} {userProfile.Lastname}";

    public static bool HasFullName(this UserProfile userProfile)
        => !string.IsNullOrWhiteSpace(userProfile.Firstname) || !string.IsNullOrWhiteSpace(userProfile.Lastname);

    public static string GetInitialLetters(this UserProfile userProfile)
    {
        var initialLetters = $"{userProfile.Firstname?.FirstOrDefault(' ')}{userProfile.Lastname?.FirstOrDefault(' ')}";

        if (string.IsNullOrWhiteSpace(initialLetters))
            initialLetters = userProfile.UserName.Value.FirstOrDefault().ToString();

        return initialLetters.ToUpper(CultureInfo.CurrentUICulture);
    }

    /// <summary>
    /// Adds user profile services.
    /// </summary>
    /// <param name="services">The <see cref="IServiceCollection"/> to add the services to.</param>
    /// <returns>The <see cref="IServiceCollection"/> so that additional calls can be chained.</returns>
    public static IServiceCollection AddProfile(this IServiceCollection services)
    {
        services.AddScoped<IUrgentProvider, DummyUrgentProvider>();
        services.AddScoped<ITodayProvider, DummyTodayProvider>();
        services.AddScoped<IThisWeekProvider, DummyThisWeekProvider>();
        services.AddScoped<ISoonProvider, DummySoonProvider>();

        return services;
    }
}
