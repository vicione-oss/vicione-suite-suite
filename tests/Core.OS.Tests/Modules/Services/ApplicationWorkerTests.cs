using System.Globalization;
using System.IO.Abstractions;
using System.IO.Abstractions.TestingHelpers;
using Core.OS.DbContext;
using Core.OS.Instance;
using Core.OS.Instance.Commands;
using Core.OS.Instance.Services;
using Core.OS.Modules.Services;
using Core.OS.Tests.Extensions;
using Core.OS.UserManagement.Extensions;
using Core.Shared.Instance.Contracts;
using Core.Shared.Instance.Requests;
using Core.Shared.Instance.Services;
using AwesomeAssertions;
using MassTransit;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;
using Sdk.Backend.Messaging;
using Sdk.Instance;
using Xunit;

namespace Core.OS.Tests.Modules.Services;

public class ApplicationWorkerTests : IClassFixture<TestApplicationFactory<EmptyTestStartup>>
{
    private readonly ISuiteMediator _mediatorMock = Substitute.For<ISuiteMediator>();
    private readonly IBusDepot _busDepot = Substitute.For<IBusDepot>();
    private readonly ISendEndpointProvider _sendEndpointProvider = Substitute.For<ISendEndpointProvider>();
    private readonly ILocalInstanceInformationProvider _localInstanceInformationMock =
        Substitute.For<ILocalInstanceInformationProvider>();
    private readonly ILogger<ApplicationWorker> _loggerMock = Substitute.For<ILogger<ApplicationWorker>>();

    private readonly WebApplicationFactory<EmptyTestStartup> _appFactory;

    public ApplicationWorkerTests(TestApplicationFactory<EmptyTestStartup> appFactory)
    {
        _appFactory = appFactory.WithWebHostBuilder(builder =>
        {
            // base setup done in factory - add/override services needed for the test
            builder.ConfigureTestServices(services =>
            {
                services
                    .AddSingleton<IFileSystem, MockFileSystem>()
                    .AddSingleton(_localInstanceInformationMock)
                    .AddSingleton(_mediatorMock)
                    .AddSingleton(_sendEndpointProvider)
                    .AddSingleton(new InMemoryClusterInformationProvider(Substitute.For<ILogger<InMemoryClusterInformationProvider>>()))
                    .AddModuleManagerWithTestModule()
                    .AddApplicationDbContextsInMemory(false)
                    .AddUserDbContextsInMemory(false)
                    .AddConnectionDbContextsInMemory(false)
                    .AddUserManagement()
                    .AddSingleton(_ => Substitute.For<INonceStore>());

                var options = Substitute.For<IOptions<MassTransitHostOptions>>();
                options.Value.Returns(new MassTransitHostOptions()
                {
                    WaitUntilStarted = true
                });
                services.AddSingleton(_busDepot);
            });
        });
    }

    [Fact]
    public async Task Starting_should_send_register_command_on_master_or_slave()
    {
        // Arrange
        _localInstanceInformationMock.SetupLocalInstanceInformation(InstanceType.Slave);
        var commandUri = MessagingHelper.GetCommandEndpointAddress<RegisterInstance>();
        var endPoint = Substitute.For<ISendEndpoint>();
        _sendEndpointProvider.GetSendEndpoint(commandUri).Returns(endPoint);

        var applicationWorker = new ApplicationWorker(_appFactory.Services, _loggerMock);

        // Act
        await applicationWorker.StartingAsync(default);

        // Assert
        await endPoint.Received().Send(Arg.Any<RegisterInstance>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Starting_should_start_bus_depot()
    {
        // Arrange
        _localInstanceInformationMock.SetupLocalInstanceInformation(InstanceType.Slave);

        var applicationWorker = new ApplicationWorker(_appFactory.Services, _loggerMock);

        // Act
        await applicationWorker.StartingAsync(default);

        // Assert
        await _busDepot.Received().Start(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Stopping_should_stop_bus_depot()
    {
        // Arrange
        _localInstanceInformationMock.SetupLocalInstanceInformation(InstanceType.Slave);

        var applicationWorker = new ApplicationWorker(_appFactory.Services, _loggerMock);

        // Act
        await applicationWorker.StoppingAsync(default);

        // Assert
        await _busDepot.Received().Stop(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Starting_should_seed_resources_and_data()
    {
        // Arrange
        _localInstanceInformationMock.SetupLocalInstanceInformation();

        _mediatorMock.Request<GetInstances, GetInstancesResponse>(Arg.Any<GetInstances>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new GetInstancesResponse([])));

        var cancellationToken = new CancellationToken();

        var worker = new ApplicationWorker(_appFactory.Services, _loggerMock);

        // Act
        await worker.StartingAsync(cancellationToken);

        // Assert
        Assert.Null(worker.InitializationErrorMessage);
    }

    [Fact]
    public async Task Starting_should_delete_orphaned_nonces()
    {
        // Arrange
        var applicationContext = _appFactory.Services.GetRequiredService<IApplicationDbContext>();
        await applicationContext.Instance.Database.MigrateAsync();

        var orphanedNonce = new Nonce { Value = Guid.NewGuid(), CreatedAt = DateTimeOffset.UtcNow.AddDays(-1) };
        applicationContext.Nonces.Add(orphanedNonce);
        await applicationContext.Instance.SaveChangesAsync();

        var nonceStore = _appFactory.Services.GetRequiredService<INonceStore>();

        _localInstanceInformationMock.SetupLocalInstanceInformation();

        _mediatorMock.Request<GetInstances, GetInstancesResponse>(Arg.Any<GetInstances>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new GetInstancesResponse([])));

        var cancellationToken = new CancellationToken();

        var worker = new ApplicationWorker(_appFactory.Services, _loggerMock);

        // Act
        await worker.StartingAsync(cancellationToken);

        // Assert
        await nonceStore.Received().DeletedOrphaned(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Starting_should_set_culture()
    {
        // Arrange
        var applicationContext = _appFactory.Services.GetRequiredService<IApplicationDbContext>();
        await applicationContext.Instance.Database.MigrateAsync();

        var applicationConfiguration = new CrossInstanceConfiguration { CultureName = "ja-JP" };
        applicationContext.CrossInstanceConfiguration.Add(applicationConfiguration);
        await applicationContext.Instance.SaveChangesAsync();

        _localInstanceInformationMock.SetupLocalInstanceInformation();

        _mediatorMock.Request<GetInstances, GetInstancesResponse>(Arg.Any<GetInstances>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new GetInstancesResponse([])));

        var cancellationToken = new CancellationToken();

        var worker = new ApplicationWorker(_appFactory.Services, _loggerMock);

        // Act
        await worker.StartingAsync(cancellationToken);

        // Assert
        CultureInfo.DefaultThreadCurrentCulture.Should().NotBeNull();
        CultureInfo.DefaultThreadCurrentCulture!.Name.Should().Be(applicationConfiguration.CultureName);

        CultureInfo.DefaultThreadCurrentUICulture.Should().NotBeNull();
        CultureInfo.DefaultThreadCurrentUICulture!.Name.Should().Be(applicationConfiguration.CultureName);
    }

    [Fact]
    public async Task Error_on_starting_should_be_provided_in_error_message()
    {
        // Arrange
        var lifetime = Substitute.For<IHostApplicationLifetime>();
        var cancellationToken = new CancellationToken();

        var serviceCollection = new ServiceCollection();
        serviceCollection.AddSingleton(lifetime);
        var applicationWorker = new ApplicationWorker(serviceCollection.BuildServiceProvider(), _loggerMock);

        await _appFactory.Services.EnsureApplicationContextIsCreated(cancellationToken);

        // Act
        await applicationWorker.StartingAsync(cancellationToken);

        // Assert
        Assert.NotNull(applicationWorker.InitializationErrorMessage);
    }
}
