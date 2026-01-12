using Blazor.Shared.UserManagement.ControlPanels.User.Extensions;
using Blazor.Shared.UserManagement.ControlPanels.Users.Extensions;
using Blazor.Shared.UserManagement.Services;
using Blazor.Shared.UserManagement.Services.Validators;
using Blazor.Shared.Validation.Extensions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Blazor.Shared.UserManagement.Extensions;

public static class IServiceCollectionExtensions
{
    public static IServiceCollection AddUserManagement(this IServiceCollection services)
    {
        services.AddScoped<IUserService, UserService>();
        services.AddTransient<IRolesProvider, RolesProvider>();
        services.AddUsersControlPanel();
        services.AddUserControlPanel();

        return services;
    }

    public static IServiceCollection AddUsernameValidator(this IServiceCollection services)
    {
        services.AddRequiredValidator();
        services.TryAddScoped<IUsernameValidator, UsernameValidator>();

        return services;
    }

    public static IServiceCollection AddPasswordValidator(this IServiceCollection services)
    {
        services.AddRequiredValidator();
        services.TryAddScoped<IPasswordValidator, PasswordValidator>();

        return services;
    }

    public static IServiceCollection AddRepeatPasswordValidator(this IServiceCollection services)
    {
        services.AddRequiredValidator();
        services.TryAddScoped<IRepeatPasswordValidator, RepeatPasswordValidator>();

        return services;
    }
}
