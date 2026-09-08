using Sdk.Backend.IO;
using Sdk.Backend.Modules;
using Sdk.Backend.Persistence;

namespace Core.OS.Persistence.Extensions;

internal static class IServiceCollectionExtensions
{
    extension(IServiceCollection services)
    {
        /// <summary>
        /// Cross-cutting persistence primitives: atomic file writing and the module
        /// DbContext registrar every concern uses to register its own context.
        /// </summary>
        internal IServiceCollection AddPersistence()
        {
            services.AddTransient<IAtomicFileWriter, AtomicFileWriter>();
            services.AddSingleton<IModuleDbContextRegistrar>(new ModuleDbContextRegistrar());

            return services;
        }
    }
}
