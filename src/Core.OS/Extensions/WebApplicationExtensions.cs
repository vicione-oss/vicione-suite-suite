using System.Security.Cryptography;
using Core.OS.Modules;
using Core.OS.Security;
using Core.OS.Security.Extensions;
using Core.OS.UserManagement.Security;
using Microsoft.Extensions.Options;
using Serilog;

namespace Core.OS.Extensions;

internal static class WebApplicationExtensions
{
    public static IEnumerable<string>? GetInvalidOptions(this WebApplication webApp)
    {
        try
        {
            var options = webApp.Services.GetRequiredService<IStartupValidator>();
            options.Validate();
        }
        catch (OptionsValidationException vex)
        {
            return vex.Failures;
        }

        return null;
    }

    public static async Task RunCoreOs(this WebApplication host)
    {
        try
        {
            // Must run before anything that inspects scheme, host or client address
            // (HSTS, https redirection, request logging, auth, antiforgery).
            host.UseForwardedHeaders();
            host.UseCookiePolicy();

            if (host.Environment.IsDevelopment())
            {
                host.UseDeveloperExceptionPage();
            }
            else
            {
                host.UseExceptionHandler("/Error");
                // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
                host.UseHsts();
            }

            host.UseHttpsRedirection();

            var moduleHost = host.Services.GetRequiredService<IModuleHost>();
            moduleHost.UseUiHost(host, host.Environment);

            host.UseResponseCachePolicy();
            var formActionOrigin = host.Services.GetRequiredService<ExternalLoginFormActionOrigin>();
            host.UseSecurityHeaders(
                context => ContentSecurityPolicy.GetBaseline(formActionOrigin.Resolve(context)),
                ContentSecurityPolicy.ReportingEndpoints);

            host.UseRouting();
            host.UseSerilogRequestLogging(options =>
            {
                options.GetLevel = (_, _, _) => Serilog.Events.LogEventLevel.Verbose;
            });

            moduleHost.UseSecurity(host);
            moduleHost.UseModuleServices(host);

#pragma warning disable ASP0014 // Suggest using top level route registrations instead of UseEndpoints
            host.UseEndpoints(moduleHost.MapModuleEndpoints);
#pragma warning restore ASP0014

            host.MapCspViolationReports();
            host.MapHealthChecks("/hc");
            host.MapGet("/liveness", () => StatusCodes.Status200OK);

            await host.RunAsync();
        }
        catch (CryptographicException e)
        {
            if (e.Source == "System.Security.Cryptography.X509Certificates")
                throw new ArgumentException("Kestrel - configured https certificate is missing", e);
        }
        catch (TaskCanceledException)
        {
            Log.Information("Main Task cancelled - Shutting down");
        }
    }
}
