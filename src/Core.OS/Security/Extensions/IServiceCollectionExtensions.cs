using Core.OS.Instance;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.HttpsPolicy;

namespace Core.OS.Security.Extensions;

internal static class IServiceCollectionExtensions
{
    extension(IServiceCollection services)
    {
        internal IServiceCollection AddTransportSecurity(InstanceOptions instanceOptions)
        {
            var safelist = TrustedProxySafelist.Parse(instanceOptions.TrustedProxies);

            services.Configure<ForwardedHeadersOptions>(options =>
            {
                options.ForwardedHeaders = ForwardedHeaders.XForwardedFor
                                           | ForwardedHeaders.XForwardedProto
                                           | ForwardedHeaders.XForwardedHost;

                // The default safelists trust loopback proxies only — exactly the packaged
                // nginx on an edge device. TrustedProxies extends them for proxies on other
                // hosts (see docs/oidc.md).
                foreach (var proxy in safelist.Proxies)
                    options.KnownProxies.Add(proxy);
                foreach (var network in safelist.Networks)
                    options.KnownIPNetworks.Add(network);

                // Empty safelists disable the source check entirely, so headers are trusted
                // from any sender. Kept only for the deprecated flag; TrustedProxies wins
                // when both are configured, as it is the stricter setting.
#pragma warning disable CS0618
                if (!instanceOptions.UseHeaderForwarding || safelist.Proxies.Count != 0 || safelist.Networks.Count != 0)
                    return;
                options.KnownIPNetworks.Clear();
                options.KnownProxies.Clear();
            });
#pragma warning restore CS0618

            // Requests only ever reach the suite over TLS (nginx on devices, https endpoints
            // elsewhere), so the Secure attribute is enforced on all cookies globally.
            services.Configure<CookiePolicyOptions>(options => options.Secure = CookieSecurePolicy.Always);

            // Mirror the Strict-Transport-Security header the packaged nginx adds, so both
            // layers advertise the same policy.
            services.Configure<HstsOptions>(options =>
            {
                options.MaxAge = TimeSpan.FromDays(365);
                options.IncludeSubDomains = true;
                options.Preload = true;
            });

            return services;
        }
    }
}
