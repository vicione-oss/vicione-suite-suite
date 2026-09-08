using Core.OS.Configuration;
using Core.OS.Extensions;
using Core.Shared.Logging;
using Microsoft.Extensions.Options;

namespace Core.OS.Logging.Extensions;

internal static class IServiceCollectionExtensions
{
    extension(IServiceCollection services)
    {
        /// <summary>
        /// Binds and validates <see cref="LoggingOptions"/> and exposes it as the
        /// <see cref="ILogOptions"/> the shared logging code depends on.
        /// </summary>
        internal IServiceCollection AddCoreLogging()
        {
            services.AddValidatedOptions<LoggingOptions, LoggingOptionsValidator>(LoggingOptions.ConfigSection);
            services.AddTransient<ILogOptions>(s => s.GetRequiredService<IOptions<LoggingOptions>>().Value);

            return services;
        }
    }
}
