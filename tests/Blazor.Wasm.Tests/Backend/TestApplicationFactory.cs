using System.Reflection;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NSubstitute;
using Sdk.Instance;
using Sdk.Testing.Backend;

namespace Blazor.Wasm.Tests.Backend;

/// <summary>
/// provides a test host containing all required services that can be modified or extended in the test classes  
/// </summary>
/// <typeparam name="TStartup"></typeparam>
public class TestApplicationFactory<TStartup> : WebApplicationFactory<TStartup> where TStartup : class
{
    /// <summary>
    /// if test needs reference to it this is available on testhost.ConfigureServices
    /// </summary>
    public IMvcBuilder? MvcBuilder { get; private set; }

    public IInstanceInformationProvider InstanceInformationProviderMock { get; } = Substitute.For<IInstanceInformationProvider>();

    /// <inheritdoc />
    protected override IHostBuilder CreateHostBuilder()
        => Host.CreateDefaultBuilder()
            .ConfigureAppConfiguration((_, conf) =>
            {
                conf.AddInMemoryCollection(new TestConfig().CurrentSettings);
            })
            .ConfigureWebHostDefaults(builder =>
            {
                builder.UseTestServer();
            });

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseContentRoot(".");

        // called before test web host extension
        builder.ConfigureServices(services =>
        {
            var assemblies = new[]
            {
                Assembly.GetExecutingAssembly()
            };

            ConfigureInstanceInformation();

            services.AddScoped(_ => InstanceInformationProviderMock);

            MvcBuilder = services.AddMvc();


            // Add ApplicationDbContext using an in-memory database for testing.
            services.AddDbContext<TestDbContext>(opts =>
            {
                opts.UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString());
            }, ServiceLifetime.Singleton, ServiceLifetime.Singleton);

            services.AddDefaultIdentity<IdentityUser>()
                .AddRoles<IdentityRole>()
                .AddEntityFrameworkStores<TestDbContext>();
        });

        // only called if test does not configure itself
        builder.Configure(app =>
        {
            app.UseHttpsRedirection();
            app.UseStaticFiles();
            app.UseRouting();

            app.UseAuthentication();
            app.UseAuthorization();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapRazorPages();
                endpoints.MapControllers();
                endpoints.MapFallbackToFile("index.html");
            });
        });
    }

    private void ConfigureInstanceInformation()
    {
        var instanceInformation = Substitute.For<IInstanceInformation>();

        instanceInformation.Id
            .Returns(Guid.NewGuid());
        instanceInformation.Name
            .Returns("TestMachine");
        InstanceInformationProviderMock.Local
            .Returns(instanceInformation);
    }

    private sealed class TestDbContext(
        DbContextOptions<TestDbContext> options) : IdentityDbContext<IdentityUser>(options);
}
