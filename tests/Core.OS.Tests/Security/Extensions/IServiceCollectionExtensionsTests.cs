using System.Net;
using Core.OS.Instance;
using Core.OS.Security.Extensions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.HttpsPolicy;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Sdk.Instance;
using IPNetwork = System.Net.IPNetwork;

namespace Core.OS.Tests.Security.Extensions;

public class IServiceCollectionExtensionsTests
{
    private static InstanceOptions CreateInstanceOptions(params string[] trustedProxies) => new()
    {
        HomeDirectory = "AppData",
        CacheDirectory = "Cache",
        BackupDirectory = "Backup",
        Type = InstanceType.Standalone,
        TrustedProxies = trustedProxies,
    };

#pragma warning disable CS0618 // keeps the deprecated flag covered until it is removed
    private static InstanceOptions CreateInstanceOptionsWithHeaderForwarding(params string[] trustedProxies)
    {
        var options = CreateInstanceOptions(trustedProxies);
        options.UseHeaderForwarding = true;
        return options;
    }
#pragma warning restore CS0618

    private static T ResolveOptions<T>(IServiceCollection services) where T : class
    {
        using var provider = services.BuildServiceProvider();
        return provider.GetRequiredService<IOptions<T>>().Value;
    }

    public class AddTransportSecurity : IServiceCollectionExtensionsTests
    {
        [Fact]
        public void Should_trust_forwarded_headers_from_loopback_only_by_default()
        {
            // Arrange
            var services = new ServiceCollection();

            // Act
            services.AddTransportSecurity(CreateInstanceOptions());

            // Assert
            var options = ResolveOptions<ForwardedHeadersOptions>(services);
            options.ForwardedHeaders.Should().Be(ForwardedHeaders.XForwardedFor
                                                 | ForwardedHeaders.XForwardedProto
                                                 | ForwardedHeaders.XForwardedHost);
            options.KnownProxies.Should().NotBeEmpty();
            options.KnownIPNetworks.Should().NotBeEmpty();
        }

        [Fact]
        public void Should_extend_the_default_safelists_with_trusted_proxies()
        {
            // Arrange
            var services = new ServiceCollection();

            // Act
            services.AddTransportSecurity(CreateInstanceOptions("203.0.113.7", "10.0.0.0/24"));

            // Assert
            var options = ResolveOptions<ForwardedHeadersOptions>(services);
            options.KnownProxies.Should().HaveCount(2).And.Contain(IPAddress.Parse("203.0.113.7"));
            options.KnownIPNetworks.Should().HaveCount(2).And.Contain(IPNetwork.Parse("10.0.0.0/24"));
        }

        [Fact]
        public void Should_reject_a_trusted_proxy_entry_that_is_neither_ip_address_nor_cidr_network()
        {
            // Arrange
            var services = new ServiceCollection();

            // Act
            var act = () => services.AddTransportSecurity(CreateInstanceOptions("proxy.example.com"));

            // Assert
            act.Should().Throw<InvalidOperationException>().WithMessage("*proxy.example.com*");
        }

        [Fact]
        public void Should_trust_forwarded_headers_from_any_source_when_the_deprecated_flag_is_set()
        {
            // Arrange
            var services = new ServiceCollection();

            // Act
            services.AddTransportSecurity(CreateInstanceOptionsWithHeaderForwarding());

            // Assert
            var options = ResolveOptions<ForwardedHeadersOptions>(services);
            options.KnownProxies.Should().BeEmpty();
            options.KnownIPNetworks.Should().BeEmpty();
        }

        [Fact]
        public void Should_keep_the_source_check_when_trusted_proxies_and_the_deprecated_flag_are_combined()
        {
            // Arrange
            var services = new ServiceCollection();

            // Act
            services.AddTransportSecurity(CreateInstanceOptionsWithHeaderForwarding("203.0.113.7"));

            // Assert
            var options = ResolveOptions<ForwardedHeadersOptions>(services);
            options.KnownProxies.Should().NotBeEmpty();
            options.KnownIPNetworks.Should().NotBeEmpty();
        }

        [Fact]
        public void Should_mark_all_cookies_secure()
        {
            // Arrange
            var services = new ServiceCollection();

            // Act
            services.AddTransportSecurity(CreateInstanceOptions());

            // Assert
            ResolveOptions<CookiePolicyOptions>(services).Secure.Should().Be(CookieSecurePolicy.Always);
        }

        [Fact]
        public void Should_match_the_packaged_nginx_hsts_policy()
        {
            // Arrange
            var services = new ServiceCollection();

            // Act
            services.AddTransportSecurity(CreateInstanceOptions());

            // Assert
            var options = ResolveOptions<HstsOptions>(services);
            options.MaxAge.Should().Be(TimeSpan.FromDays(365));
            options.IncludeSubDomains.Should().BeTrue();
            options.Preload.Should().BeTrue();
        }
    }

    public class TransportSecurityPipeline : IServiceCollectionExtensionsTests
    {
        [Fact]
        public async Task Should_apply_forwarded_proto_from_a_loopback_proxy()
        {
            // Arrange
            await using var app = await StartApp(CreateInstanceOptions(), IPAddress.Loopback);
            using var client = CreateClientWithForwardedProto(app);

            // Act
            var scheme = await client.GetStringAsync("/scheme", TestContext.Current.CancellationToken);

            // Assert
            scheme.Should().Be("https");
        }

        [Fact]
        public async Task Should_ignore_forwarded_proto_from_a_remote_sender()
        {
            // Arrange
            await using var app = await StartApp(CreateInstanceOptions(), IPAddress.Parse("203.0.113.7"));
            using var client = CreateClientWithForwardedProto(app);

            // Act
            var scheme = await client.GetStringAsync("/scheme", TestContext.Current.CancellationToken);

            // Assert
            scheme.Should().Be("http");
        }

        [Fact]
        public async Task Should_apply_forwarded_proto_from_a_trusted_remote_proxy()
        {
            // Arrange
            await using var app = await StartApp(CreateInstanceOptions("203.0.113.7"), IPAddress.Parse("203.0.113.7"));
            using var client = CreateClientWithForwardedProto(app);

            // Act
            var scheme = await client.GetStringAsync("/scheme", TestContext.Current.CancellationToken);

            // Assert
            scheme.Should().Be("https");
        }

        [Fact]
        public async Task Should_ignore_forwarded_proto_from_a_remote_sender_not_listed_as_trusted_proxy()
        {
            // Arrange
            await using var app = await StartApp(CreateInstanceOptions("203.0.113.7"), IPAddress.Parse("203.0.113.9"));
            using var client = CreateClientWithForwardedProto(app);

            // Act
            var scheme = await client.GetStringAsync("/scheme", TestContext.Current.CancellationToken);

            // Assert
            scheme.Should().Be("http");
        }

        [Fact]
        public async Task Should_apply_forwarded_proto_from_any_source_when_the_deprecated_flag_is_set()
        {
            // Arrange
            await using var app = await StartApp(CreateInstanceOptionsWithHeaderForwarding(), IPAddress.Parse("203.0.113.7"));
            using var client = CreateClientWithForwardedProto(app);

            // Act
            var scheme = await client.GetStringAsync("/scheme", TestContext.Current.CancellationToken);

            // Assert
            scheme.Should().Be("https");
        }

        [Fact]
        public async Task Should_mark_response_cookies_secure()
        {
            // Arrange
            await using var app = await StartApp(CreateInstanceOptions(), IPAddress.Loopback);
            using var client = app.GetTestClient();

            // Act
            var response = await client.GetAsync("/cookie", TestContext.Current.CancellationToken);

            // Assert
            response.Headers.GetValues("Set-Cookie").Should().ContainSingle()
                .Which.Should().ContainEquivalentOf("secure");
        }

        private static async Task<WebApplication> StartApp(InstanceOptions instanceOptions, IPAddress remoteAddress)
        {
            var builder = WebApplication.CreateBuilder();
            builder.WebHost.UseTestServer();
            builder.Services.AddTransportSecurity(instanceOptions);

            var app = builder.Build();
            // TestServer connections carry no remote address, so fake the proxy's one
            // before the forwarded-headers middleware checks it against its safelists.
            app.Use(async (context, next) =>
            {
                context.Connection.RemoteIpAddress = remoteAddress;
                await next();
            });
            app.UseForwardedHeaders();
            app.UseCookiePolicy();
            app.MapGet("/scheme", context => context.Response.WriteAsync(context.Request.Scheme));
            app.MapGet("/cookie", context =>
            {
                context.Response.Cookies.Append("test", "value");
                return Task.CompletedTask;
            });

            await app.StartAsync(TestContext.Current.CancellationToken);
            return app;
        }

        private static HttpClient CreateClientWithForwardedProto(WebApplication app)
        {
            var client = app.GetTestClient();
            client.DefaultRequestHeaders.Add("X-Forwarded-Proto", "https");
            return client;
        }
    }
}
