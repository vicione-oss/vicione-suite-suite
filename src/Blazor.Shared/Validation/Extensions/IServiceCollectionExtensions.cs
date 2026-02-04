using Blazor.Shared.Validation.Services.Validators;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Blazor.Shared.Validation.Extensions;

public static class IServiceCollectionExtensions
{
    extension(IServiceCollection services)
    {
        public IServiceCollection AddRequiredValidator()
        {
            services.TryAddScoped<IRequiredValidator, RequiredValidator>();

            return services;
        }

        public IServiceCollection AddEmailValidator()
        {
            services.TryAddScoped<IEmailValidator, EmailValidator>();

            return services;
        }

        public IServiceCollection AddPhoneNumberValidator()
        {
            services.TryAddScoped<IPhoneNumberValidator, PhoneNumberValidator>(); //Should be own method

            return services;
        }
    }
}
