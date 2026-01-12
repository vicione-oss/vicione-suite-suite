using System.IO.Abstractions;
using Core.Module.Comparer;
using Core.OS.Instance;
using Core.OS.Instance.Extensions;
using Microsoft.Extensions.Options;

namespace Core.OS.Hosting.Extensions;

internal static class WebApplicationBuilderExtensions
{
    public static async Task<bool> DetectVersionDowngrade(this WebApplicationBuilder builder, IFileSystem fileSystem, InstanceOptions options, Serilog.ILogger logger, CancellationToken cancellationToken = default)
    {
        try
        {
            var suiteVersionString = SuiteVersionUtils.GetSuiteVersion();

            if (!fileSystem.DataVersionFileExists(options))
            {
                await fileSystem.WriteDataVersionFile(options, suiteVersionString, cancellationToken);
                logger.Information("Initialized data version to '{SuiteVersion}'", suiteVersionString);
                return false;
            }

            var persistedVersionString = await fileSystem.ReadDataVersionFile(options, cancellationToken);
            if (string.IsNullOrWhiteSpace(persistedVersionString))
            {
                // should never happen but for sanity we need try to fix it to current version
                await fileSystem.WriteDataVersionFile(options, suiteVersionString, cancellationToken);
                logger.Warning("Empty data version restored to '{SuiteVersion}'", suiteVersionString);
                throw new InvalidOperationException($"Empty data version. Try restore to {suiteVersionString}");
            }

            var compareResult = new StringVersionComparer().Compare(suiteVersionString, persistedVersionString);
            if (compareResult == 0)
                return false;

            // is persisted version higher then running one?
            if (compareResult > 0)
            {
                await fileSystem.WriteDataVersionFile(options, suiteVersionString, cancellationToken);
                logger.Warning("Updated data version to '{SuiteVersion}'", suiteVersionString);
                return false;
            }

            // if we confirm to Semver and do our releases that way we could ensure that patch versions would work
            // because a data migration would lead to a minor version change at least
            if (SuiteVersionUtils.IsPatchUpdate(suiteVersionString, persistedVersionString))
            {
                await fileSystem.WriteDataVersionFile(options, suiteVersionString, cancellationToken);
                logger.Warning("Downgraded data version to '{SuiteVersion}'", suiteVersionString);
                return false;
            }

            logger.Warning("Software version should be updated from '{SuiteVersion}' to '{PersistedVersion}'", suiteVersionString, persistedVersionString);

            builder.Services.AddSingleton(fileSystem);
            builder.Services.AddSingleton(logger);
            builder.Services.AddSingleton(Options.Create(options));

            // create the simplest host possible to display the errors
            var host = builder.Build();
            host.UseHttpsRedirection();

            // we have a page with to options (links -> reset or exit)
            host.MapGet("/", () => HttpResultFactory.VersionDowngradeDetected(suiteVersionString, persistedVersionString));
            host.MapGet("/reset", DowngradeReset);
            host.MapGet("/exit", DowngradeExit);

            // run the host what will stop the program workflow here
            await host.RunAsync(cancellationToken);
        }
        catch (Exception e)
        {
            logger.Error(e, "Error on detecting downgrade");
        }
        return true;
    }

    /// <summary>
    /// Sets the reset file and stops the application
    /// </summary>
    /// <param name="http"></param>
    private static IResult DowngradeReset(HttpContext http)
    {
        var logger = http.RequestServices.GetRequiredService<Serilog.ILogger>();
        logger.Warning("Suite reset requested by user because of detected version downgrade");

        var fileSystem = http.RequestServices.GetRequiredService<IFileSystem>();
        var options = http.RequestServices.GetRequiredService<IOptions<InstanceOptions>>();
        fileSystem.WriteResetFile(options.Value);

        var applicationLifetime = http.RequestServices.GetRequiredService<IHostApplicationLifetime>();
        applicationLifetime.StopApplication();

        return Results.Redirect("/");
    }

    /// <summary>
    /// Just stops the application
    /// </summary>
    /// <param name="http"></param>
    private static IResult DowngradeExit(HttpContext http)
    {
        var logger = http.RequestServices.GetRequiredService<Serilog.ILogger>();
        logger.Information("Suite shutdown requested by user because of detected version downgrade");
        var applicationLifetime = http.RequestServices.GetRequiredService<IHostApplicationLifetime>();
        applicationLifetime.StopApplication();

        return Results.Redirect("/");
    }
}
