using System.Globalization;
using System.IO.Abstractions;
using System.IO.Abstractions.TestingHelpers;
using AwesomeAssertions;
using Core.OS.DbContext;
using Core.OS.Instance;
using Core.OS.Instance.Commands;
using Core.OS.Instance.Contracts;
using Core.OS.Instance.Services;
using Core.OS.Modules;
using Core.OS.Modules.Services;
using Core.OS.Tests.Extensions;
using Core.OS.UserManagement.Extensions;
using Core.Shared.Instance.Contracts;
using Core.Shared.Instance.Requests;
using Core.Shared.Instance.Services;
using Core.UiHosting;
using MassTransit;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;
using OpenTelemetry.Trace;
using Sdk.Backend.Messaging;
using Sdk.Instance;
using TestUiHost;
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
                    .AddSingleton(_ => Substitute.For<INonceStore>())
                    .AddSingleton<SynchronizationState>(_ =>
                    {
                        var syncDone = new SynchronizationState();
                        syncDone.CompleteSynchronization();
                        return syncDone;
                    });

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

        var applicationWorker = CreateApplicationWorker();

        // Act
        await applicationWorker.StartingAsync(TestContext.Current.CancellationToken);

        // Assert
        await endPoint.Received().Send(Arg.Any<RegisterInstance>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Starting_should_start_bus_depot()
    {
        // Arrange
        _localInstanceInformationMock.SetupLocalInstanceInformation(InstanceType.Slave);

        var applicationWorker = CreateApplicationWorker();

        // Act
        await applicationWorker.StartingAsync(TestContext.Current.CancellationToken);

        // Assert
        await _busDepot.Received().Start(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Stopping_should_stop_bus_depot()
    {
        // Arrange
        _localInstanceInformationMock.SetupLocalInstanceInformation(InstanceType.Slave);

        var applicationWorker = CreateApplicationWorker();

        // Act
        await applicationWorker.StoppingAsync(TestContext.Current.CancellationToken);

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

        var worker = CreateApplicationWorker();

        // Act
        await worker.StartingAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.Null(worker.InitializationErrorMessage);
    }

    [Fact]
    public async Task Starting_should_delete_orphaned_nonces()
    {
        // Arrange
        var applicationContext = _appFactory.Services.GetRequiredService<IApplicationDbContext>();
        await applicationContext.MigrateAsync(TestContext.Current.CancellationToken);

        var orphanedNonce = new Nonce { Value = Guid.NewGuid(), CreatedAt = DateTimeOffset.UtcNow.AddDays(-1) };
        applicationContext.Nonces.Add(orphanedNonce);
        await applicationContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        var nonceStore = _appFactory.Services.GetRequiredService<INonceStore>();

        _localInstanceInformationMock.SetupLocalInstanceInformation();

        _mediatorMock.Request<GetInstances, GetInstancesResponse>(Arg.Any<GetInstances>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new GetInstancesResponse([])));

        var worker = CreateApplicationWorker();

        // Act
        await worker.StartingAsync(TestContext.Current.CancellationToken);

        // Assert
        await nonceStore.Received().DeletedOrphaned(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Starting_should_set_culture()
    {
        // Arrange
        var applicationContext = _appFactory.Services.GetRequiredService<IApplicationDbContext>();
        await applicationContext.MigrateAsync(TestContext.Current.CancellationToken);

        var applicationConfiguration = new CrossInstanceConfiguration { CultureName = "ja-JP" };
        applicationContext.CrossInstanceConfiguration.Add(applicationConfiguration);
        await applicationContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        var moduleHost = _appFactory.Services.GetRequiredService<IModuleHost>();
        var module = Substitute.For<IUiHostModule>();
        var host = new TestUiHostBackend();
        moduleHost.GetModules().Returns([host]);

        _localInstanceInformationMock.SetupLocalInstanceInformation();

        _mediatorMock.Request<GetInstances, GetInstancesResponse>(Arg.Any<GetInstances>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new GetInstancesResponse([])));
        var worker = CreateApplicationWorker();

        // Act
        await worker.StartingAsync(TestContext.Current.CancellationToken);

        // Assert
        CultureInfo.DefaultThreadCurrentCulture.Should().NotBeNull();
        CultureInfo.DefaultThreadCurrentCulture.Name.Should().Be(applicationConfiguration.CultureName);

        CultureInfo.DefaultThreadCurrentUICulture.Should().NotBeNull();
        CultureInfo.DefaultThreadCurrentUICulture.Name.Should().Be(applicationConfiguration.CultureName);

        var uiHost = (IUiHostModule?)moduleHost.GetModules().FirstOrDefault(k => k is IUiHostModule);
        uiHost.Should().NotBeNull();
        uiHost.GetDefaultRequestCulture().Should().Be("ja-JP");
    }

    [Fact]
    public async Task Error_on_starting_should_be_provided_in_error_message()
    {
        // Arrange
        var lifetime = Substitute.For<IHostApplicationLifetime>();

        var serviceCollection = new ServiceCollection();
        serviceCollection.AddSingleton(lifetime);
        serviceCollection.AddSingleton(Substitute.For<TracerProvider>());
        await using var services = serviceCollection.BuildServiceProvider();
        var applicationWorker = CreateApplicationWorker();

        await _appFactory.Services.EnsureApplicationContextIsCreated(TestContext.Current.CancellationToken);

        // Act
        await applicationWorker.StartingAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(applicationWorker.InitializationErrorMessage);
    }

    private ApplicationWorker CreateApplicationWorker()
        => new(_appFactory.Services, _loggerMock);
}
