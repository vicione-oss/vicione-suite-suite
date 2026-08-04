using System.IO.Abstractions;
using System.IO.Abstractions.TestingHelpers;
using Core.OS.DbContext;
using Core.OS.HostManagement;
using Core.OS.Instance;
using Core.OS.Instance.Consumers;
using Core.OS.Tests.Extensions;
using Core.OS.Tests.HostManagement.Extensions;
using Core.Shared.Instance.Commands;
using Core.Shared.Instance.Contracts;
using HostManagement.Shared.Communication;
using HostManagement.Shared.Communication.Enums;
using MassTransit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Sdk.Backend.Persistence;
using Sdk.Instance;
using Sdk.Testing.Backend;

namespace Core.OS.Tests.Instance.Consumers;

public class ControlInstanceConsumerTests : TestWithDbContextSqlite<ApplicationDbContextSqlite>
{
    private readonly Action<IBusRegistrationConfigurator> _configureServices;
    private readonly IPipeClient _pipeClient = Substitute.For<IPipeClient>();
    private readonly ILocalInstanceInformationProvider _localInstanceProvider = Substitute.For<ILocalInstanceInformationProvider>();
    private readonly Guid _instanceId = Guid.NewGuid();
    private readonly MockFileSystem _fileSystem = new();

    private readonly InstanceInformation _masterInstance = new()
    {
        Id = Guid.NewGuid(),
        Type = InstanceType.Master,
        InstalledModules = [Shared.Constants.SystemModuleId]
    };

    private readonly InstanceInformation _standaloneInstance = new()
    {
        Id = Guid.NewGuid(),
        Type = InstanceType.Standalone,
        InstalledModules = [Shared.Constants.SystemModuleId]
    };

    private readonly InstanceOptions _instanceOptions = new()
    {
        BackupDirectory = "backup",
        CacheDirectory = "cache",
        HomeDirectory = "home",
        Type = InstanceType.Standalone,
    };

    public ControlInstanceConsumerTests()
    {
        _localInstanceProvider.Local
            .Returns(_masterInstance);

        _configureServices = cfg =>
        {
            cfg.AddConsumer<ControlInstanceConsumer>();
            cfg.AddSingleton(_localInstanceProvider);
            cfg.AddSingleton(_pipeClient);
            cfg.AddSingleton<IFileSystem>(_fileSystem);
            cfg.AddSingleton(Options.Create(_instanceOptions));
            cfg.AddSingleton<IApplicationDbContext>(_ => TestDbContext);
            cfg.AddSingleton(new ModuleContextTypeInformation(Shared.Constants.SystemModuleId, typeof(ApplicationDbContext), typeof(ApplicationDbContext).AssemblyQualifiedName!));
            cfg.AddRoutingSlipBuilderFactory();
        };
    }

    public sealed class Delete : ControlInstanceConsumerTests
    {
        [Fact]
        public async Task Should_consume_command()
        {
            // Arrange
            await using var tester = new MassTransitTester(_configureServices);
            var command = new ControlInstance
            {
                Action = InstanceCommand.Delete,
                InstanceId = Guid.NewGuid()
            };

            // Act + Assert
            await tester.TestInstanceDependentCommand<ControlInstance, ControlInstanceConsumer>(command);
        }
    }

    public sealed class Restart : ControlInstanceConsumerTests
    {

        [Fact]
        public async Task Should_send_restart_service_request_to_host_management()
        {
            // Arrange
            await using var tester = new MassTransitTester(_configureServices);
            var appDb = tester.Services.GetRequiredService<IApplicationDbContext>();
            appDb.InstanceInfo.Add(_standaloneInstance);
            await appDb.SaveChangesAsync(TestContext.Current.CancellationToken);

            _pipeClient.SetupRestartServiceResult(OperationStatus.Success, "TEST");
            _localInstanceProvider.SetupLocalInstanceInformation(guid: _instanceId);

            var command = new ControlInstance()
            {
                Action = InstanceCommand.Restart,
                InstanceId = _standaloneInstance.Id,
            };

            // Act
            await tester.TestInstanceDependentCommand<ControlInstance, ControlInstanceConsumer>(command);

            // Assert
            await _pipeClient.Received(1).SendRequest(Topics.RestartService, _instanceOptions.ServiceName, Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task Should_skip_application_shutdown_with_wrong_instance_id()
        {
            // Arrange
            await using var tester = new MassTransitTester(_configureServices);
            _localInstanceProvider.SetupLocalInstanceInformation(guid: _instanceId);
            var command = new ControlInstance()
            {
                Action = InstanceCommand.Restart,
                InstanceId = Guid.NewGuid(),
            };

            // Act
            await tester.TestInstanceDependentCommand<ControlInstance, ControlInstanceConsumer>(command);

            // Assert
            await _pipeClient.Received(0).SendRequest(Topics.RestartService, _instanceOptions.ServiceName, Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task Should_create_suite_restart_file()
        {
            // Arrange
            await using var tester = new MassTransitTester(_configureServices);
            var appDb = tester.Services.GetRequiredService<IApplicationDbContext>();
            appDb.InstanceInfo.Add(_standaloneInstance);
            await appDb.SaveChangesAsync(TestContext.Current.CancellationToken);
            _localInstanceProvider.SetupLocalInstanceInformation(guid: _instanceId);

            var command = new ControlInstance()
            {
                Action = InstanceCommand.Restart,
                InstanceId = _standaloneInstance.Id,
            };

            // Act
            await tester.TestInstanceDependentCommand<ControlInstance, ControlInstanceConsumer>(command);

            // Assert
            _fileSystem.File.Exists("/run/vicione-suite/suite-ui-restart").Should().BeTrue();
        }
    }

    public sealed class Synchronize : ControlInstanceConsumerTests
    {
        [Fact]
        public async Task Should_consume_command()
        {
            // Arrange
            await using var tester = new MassTransitTester(_configureServices);
            var command = new ControlInstance
            {
                Action = InstanceCommand.Synchronize,
                InstanceId = Guid.NewGuid()
            };

            // Act + Assert
            await tester.TestInstanceDependentCommand<ControlInstance, ControlInstanceConsumer>(command);
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _pipeClient.Dispose();
        }
        base.Dispose(disposing);
    }
}
