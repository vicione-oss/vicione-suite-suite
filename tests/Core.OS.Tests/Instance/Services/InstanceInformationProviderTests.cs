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
using Sdk.Backend.Messaging;
using Sdk.Instance;
using Sdk.Testing.Backend;

namespace Core.OS.Tests.Instance.Services;

public sealed class InstanceInformationProviderTests : IClassFixture<TestApplicationFactory<EmptyTestStartup>>
{
    private readonly IConfiguration _config;
    private readonly MockFileSystem _fileSystem = new();
    private readonly WebApplicationFactory<EmptyTestStartup> _appFactory;
    private readonly ILocalInstanceInformationProvider _localInstanceInformationMock =
        Substitute.For<ILocalInstanceInformationProvider>();

    private readonly IModuleMetadataProvider _metadataProvider = Substitute.For<IModuleMetadataProvider>();
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
                services.AddSingleton(_metadataProvider);
                services.AddScoped(_ => Substitute.For<ISuiteMediator>());
                services.AddModuleManagerWithTestModule();
                services.AddApplicationDbContextsInMemory();
                services.AddScoped<InstanceInformationProvider>();
            });
        });
    }

    [Fact]
    public async Task Should_create_master_instance_id_on_first_get()
    {
        // Arrange
        _localInstanceInformationMock.SetupLocalInstanceInformation(InstanceType.Master);

        var provider = await CreateInstanceInformationProvider(_config, TestContext.Current.CancellationToken);

        // Act
        var id = provider.Local.Id;

        // Assert
        id.Should().Be(Constants.MasterInstanceGuid);
    }

    [Fact]
    public async Task Should_have_only_one_master_instance_exist()
    {
        // Arrange
        _localInstanceInformationMock.SetupLocalInstanceInformation(InstanceType.Master);

        var provider1 = await CreateInstanceInformationProvider(_config, TestContext.Current.CancellationToken);
        var provider2 = await CreateInstanceInformationProvider(_config, TestContext.Current.CancellationToken);

        // Act
        var id1 = provider1.Local.Id;
        var id2 = provider2.Local.Id;

        // Assert
        id1.Should().Be(id2);
    }

    [Fact]
    public async Task Should_create_standalone_instance_id_on_first_get()
    {
        // Arrange
        _localInstanceInformationMock.SetupLocalInstanceInformation();

        var provider = await CreateInstanceInformationProvider(_config, TestContext.Current.CancellationToken);

        // Act
        var id = provider.Local.Id;

        // Assert
        id.Should().NotBe(Guid.Empty);
    }

    [Fact]
    public async Task Should_set_standalone_instance_id_by_config()
    {
        // Arrange
        var preloadId = Guid.NewGuid();
        _localInstanceInformationMock.SetupLocalInstanceInformation(InstanceType.Standalone, preloadId);

        var provider = await CreateInstanceInformationProvider(_config, TestContext.Current.CancellationToken);

        // Act
        var id = provider.Local.Id;

        // Assert
        id.Should().Be(preloadId);
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
