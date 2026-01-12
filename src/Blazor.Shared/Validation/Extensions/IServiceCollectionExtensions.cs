using Blazor.Shared.Validation.Services.Validators;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Blazor.Shared.Validation.Extensions;

public static class IServiceCollectionExtensions
{
    public static IServiceCollection AddRequiredValidator(this IServiceCollection services)
    {
        services.TryAddScoped<IRequiredValidator, RequiredValidator>();

        return services;
    }

    public static IServiceCollection AddEmailValidator(this IServiceCollection services)
    {
        services.TryAddScoped<IEmailValidator, EmailValidator>();

        return services;
    }

    public static IServiceCollection AddPhoneNumberValidator(this IServiceCollection services)
    {
        services.TryAddScoped<IPhoneNumberValidator, PhoneNumberValidator>(); //Should be own method

        return services;
    }
}
