using System.Reflection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NSubstitute;
using Sdk.Instance;
using Sdk.Testing.Backend;

namespace Core.OS.Tests;

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

            MvcBuilder = services.AddTestSetupMvc(assemblies);
        });

        // only called if test does not configure itself
        builder.Configure(app =>
        {
            app.ConfigureTestSetup();
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
}
