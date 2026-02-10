using System.IO.Abstractions.TestingHelpers;
using Core.OS.Instance;
using Core.OS.Instance.Extensions;
using Core.OS.Instance.Services;
using Core.OS.Modules;
using Core.OS.Tests.Extensions;
using Core.Shared;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Sdk.Backend.Messaging;
using Sdk.Instance;
using Sdk.Testing.Backend;
using Xunit;

namespace Core.OS.Tests.Instance.Services;

public class InstanceInformationProviderTests : IClassFixture<TestApplicationFactory<EmptyTestStartup>>
{
    private readonly IConfiguration _config;
    private readonly MockFileSystem _fileSystem = new();
    private readonly WebApplicationFactory<EmptyTestStartup> _appFactory;
    private readonly ILocalInstanceInformationProvider _localInstanceInformationMock =
        Substitute.For<ILocalInstanceInformationProvider>();

    private readonly IModuleMetadataCache _metadataCache = Substitute.For<IModuleMetadataCache>();
    public InstanceInformationProviderTests(TestApplicationFactory<EmptyTestStartup> appFactory)
    {
        _config = new TestConfig().BuildConfiguration();
        _appFactory = appFactory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                services
                    .AddTestIdentity()
                    .AddSingleton(_localInstanceInformationMock);
            });
            builder.ConfigureTestServices(services =>
            {
                services.ReplaceConfiguration(_config);
                services.AddSingleton(_metadataCache);
                services.AddScoped(_ => Substitute.For<ISuiteMediator>());
                services.AddModuleManagerWithTestModule();
                services.AddApplicationDbContextsInMemory();
                services.AddApplicationDbContextsInMemory();
                services.AddScoped<InstanceInformationProvider>();
            });
        });
    }

    [Fact]
    public async Task MasterInstanceIdGetsCreatedOnFirstGet()
    {
        // Arrange
        _localInstanceInformationMock.SetupLocalInstanceInformation(InstanceType.Master);

        var provider = await CreateInstanceInformationProvider(_config, TestContext.Current.CancellationToken);

        // Act
        var id = provider.Local.Id;

        // Assert
        Assert.Equal(Constants.MasterInstanceGuid, id);
    }

    [Fact]
    public async Task MasterInstanceOnlyOneCanExist()
    {
        // Arrange
        _localInstanceInformationMock.SetupLocalInstanceInformation(InstanceType.Master);

        var provider1 = await CreateInstanceInformationProvider(_config, TestContext.Current.CancellationToken);
        var provider2 = await CreateInstanceInformationProvider(_config, TestContext.Current.CancellationToken);

        // Act
        var id1 = provider1.Local.Id;
        var id2 = provider2.Local.Id;

        // Assert
        Assert.Equal(id1, id2);
    }

    [Fact]
    public async Task StandaloneInstanceIdGetsCreatedOnFirstGet()
    {
        // Arrange
        _localInstanceInformationMock.SetupLocalInstanceInformation();

        var provider = await CreateInstanceInformationProvider(_config, TestContext.Current.CancellationToken);

        // Act
        var id = provider.Local.Id;

        // Assert
        Assert.True(id != Guid.Empty);
    }

    [Fact]
    public async Task StandaloneInstanceIdSetByConfig()
    {
        // Arrange
        var preloadId = Guid.NewGuid();
        _localInstanceInformationMock.SetupLocalInstanceInformation(InstanceType.Standalone, preloadId);

        var provider = await CreateInstanceInformationProvider(_config, TestContext.Current.CancellationToken);

        // Act
        var id = provider.Local.Id;

        // Assert
        Assert.Equal(preloadId, id);
    }

    private async Task<InstanceInformationProvider> CreateInstanceInformationProvider(IConfiguration config, CancellationToken cancellationToken)
    {
        _fileSystem.EnsureInstanceIdFile(config.GetInstanceOptions());

        using var scope = _appFactory.Services.CreateScope();
        var scopedServices = scope.ServiceProvider;

        await scopedServices.EnsureApplicationContextIsCreated(cancellationToken);

        return scopedServices.GetRequiredService<InstanceInformationProvider>();
    }
}
