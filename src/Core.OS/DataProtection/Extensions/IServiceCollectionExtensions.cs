using System.IO.Abstractions;
using Microsoft.AspNetCore.DataProtection.KeyManagement;
using Microsoft.AspNetCore.DataProtection.Repositories;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Core.OS.DataProtection.Extensions;

public static class IServiceCollectionExtensions
{
    internal static IServiceCollection ConfigureDataProtection(this IServiceCollection services)
        => services.AddSingleton<IConfigureOptions<KeyManagementOptions>>(services =>
        {
            var loggerFactory = services.GetService<ILoggerFactory>() ?? NullLoggerFactory.Instance;
            return new ConfigureOptions<KeyManagementOptions>(options =>
            {
                options.XmlRepository = new SafeXmlRepository(new FileSystem(),
                    (FileSystemXmlRepository.DefaultKeyStorageDirectory ?? throw new InvalidOperationException("No default xml directory.")).FullName,
                    loggerFactory);
            });
        });
}
