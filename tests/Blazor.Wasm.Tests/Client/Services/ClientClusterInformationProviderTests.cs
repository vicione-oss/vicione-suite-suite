using Blazor.Wasm.Client.Services;
using Core.Shared.Instance.Commands;
using Core.Shared.Instance.Contracts;
using Core.Shared.Instance.Events;
using Core.Shared.Instance.HealthCheck;
using Core.Shared.Instance.Requests;
using AwesomeAssertions;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using NSubstitute;
using Sdk.Client.Infrastructure;
using Sdk.Instance;
using Sdk.Instance.Events;
using Xunit;

namespace Blazor.Wasm.Tests.Client.Services;

public class ClientClusterInformationProviderTests
{
    public class Initialize
    {
        [Fact]
        public async Task Adds_instances()
        {
            var guid = Guid.NewGuid();
            var mediator = Substitute.For<IUiMediator>();
            using var clusterInformationProvider = new ClientClusterInformationProvider(mediator);

            mediator.Request<GetInstances, GetInstancesResponse>(Arg.Any<GetInstances>(), Arg.Any<CancellationToken>())
                .Returns(new GetInstancesResponse(
                [
                    new InstanceInformation()
                    {
                        Id = guid,
                        Name = "Test",
                        Type = InstanceType.Master,
                    },
                ]));

            var result = await clusterInformationProvider.GetInstancesInCluster(CancellationToken.None);

            var instance = result.Single();
            instance.Id.Should().Be(guid);
            instance.Name.Should().Be("Test");
            instance.Type.Should().Be(InstanceType.Master);
        }
    }

    public class GetHealthStatus
    {
        [Fact]
        public async Task Returns_correct_health_status()
        {
            var guid = Guid.NewGuid();
            var mediator = Substitute.For<IUiMediator>();
            using var clusterInformationProvider = new ClientClusterInformationProvider(mediator);

            mediator.Request<GetInstances, GetInstancesResponse>(Arg.Any<GetInstances>(), Arg.Any<CancellationToken>())
                .Returns(new GetInstancesResponse(
                [
                    new InstanceInformation()
                    {
                        Id = guid,
                        Name = "Test",
                        Type = InstanceType.Master,
                    },
                ]));

            await clusterInformationProvider.GetInstancesInCluster(CancellationToken.None);

            var result = await clusterInformationProvider.GetHealthStatus(guid);

            result.Should().BeNull();
        }

        [Fact]
        public async Task Returns_null_when_provider_is_not_initialized()
        {
            var guid = Guid.NewGuid();
            var mediator = Substitute.For<IUiMediator>();
            using var clusterInformationProvider = new ClientClusterInformationProvider(mediator);

            mediator.Request<GetInstances, GetInstancesResponse>(Arg.Any<GetInstances>(), Arg.Any<CancellationToken>())
                .Returns(new GetInstancesResponse(
                [
                    new InstanceInformation()
                    {
                        Id = guid,
                        Name = "Test",
                        Type = InstanceType.Master,
                    },
                ]));

            var result = await clusterInformationProvider.GetHealthStatus(guid);

            result.Should().BeNull();
        }
    }

    public class GetInstancesInCluster
    {
        [Fact]
        public async Task Returns_correct_list()
        {
            var master = new InstanceInformation()
            {
                Id = Guid.NewGuid(),
                Name = "Test_Master",
                Type = InstanceType.Master,
            };
            var slave = new InstanceInformation()
            {
                Id = Guid.NewGuid(),
                Name = "Test_Slave",
                Type = InstanceType.Slave,
            };

            var mediator = Substitute.For<IUiMediator>();
            using var clusterInformationProvider = new ClientClusterInformationProvider(mediator);

            mediator.Request<GetInstances, GetInstancesResponse>(Arg.Any<GetInstances>(), Arg.Any<CancellationToken>())
                .Returns(new GetInstancesResponse([master, slave,]));

            await clusterInformationProvider.GetInstancesInCluster(CancellationToken.None);

            var result = await clusterInformationProvider.GetInstancesInCluster(CancellationToken.None);

            result.Count.Should().Be(2);
            result.Should().Contain(master);
            result.Should().Contain(slave);
        }

        [Fact]
        public async Task Returns_empty_list()
        {
            var mediator = Substitute.For<IUiMediator>();
            using var clusterInformationProvider = new ClientClusterInformationProvider(mediator);

            mediator.Request<GetInstances, GetInstancesResponse>(Arg.Any<GetInstances>(), Arg.Any<CancellationToken>())
                .Returns(new GetInstancesResponse([]));

            var result = await clusterInformationProvider.GetInstancesInCluster(CancellationToken.None);

            result.Should().BeEmpty();
        }
    }

    public class IsClusterHealthy
    {
        [Theory]
        [InlineData(HealthStatus.Healthy, HealthStatus.Healthy, true)]
        [InlineData(HealthStatus.Healthy, HealthStatus.Unhealthy, false)]
        [InlineData(HealthStatus.Unhealthy, HealthStatus.Unhealthy, false)]
        [InlineData(HealthStatus.Healthy, null, false)]
        [InlineData(null, null, false)]
        public async Task Returns_correct_value(HealthStatus? masterHealthStatus, HealthStatus? slaveHealthStatus, bool expected)
        {
            var guidMaster = Guid.NewGuid();
            var guidSlave = Guid.NewGuid();
            var mediator = Substitute.For<IUiMediator>();
            using var clusterInformationProvider = new ClientClusterInformationProvider(mediator);

            mediator.Request<GetInstances, GetInstancesResponse>(Arg.Any<GetInstances>(), Arg.Any<CancellationToken>())
                .Returns(new GetInstancesResponse(
                [
                    new InstanceInformation()
                    {
                        Id = guidMaster,
                        Name = "Test_Master",
                        Type = InstanceType.Master,
                    },
                    new InstanceInformation()
                    {
                        Id = guidSlave,
                        Name = "Test_Slave",
                        Type = InstanceType.Slave,
                    },
                ]));

            await clusterInformationProvider.GetInstancesInCluster(CancellationToken.None);

            if (masterHealthStatus is not null)
            {
                await clusterInformationProvider.Consume(
                    new ClientContext<InstanceHealthInfo>(
                        new(guidMaster, DateTime.Now, (HealthStatus)masterHealthStatus),
                        null),
                    CancellationToken.None);
            }

            if (slaveHealthStatus is not null)
            {
                await clusterInformationProvider.Consume(
                    new ClientContext<InstanceHealthInfo>(
                        new(guidSlave, DateTime.Now, (HealthStatus)slaveHealthStatus),
                        null),
                    CancellationToken.None);
            }

            var result = await clusterInformationProvider.IsClusterHealthy();

            result.Should().Be(expected);
        }

        [Fact]
        public async Task Returns_false_when_provider_is_not_initialized()
        {
            var guidMaster = Guid.NewGuid();
            var guidSlave = Guid.NewGuid();
            var mediator = Substitute.For<IUiMediator>();
            using var clusterInformationProvider = new ClientClusterInformationProvider(mediator);

            mediator.Request<GetInstances, GetInstancesResponse>(Arg.Any<GetInstances>(), Arg.Any<CancellationToken>())
                .Returns(new GetInstancesResponse(
                [
                    new InstanceInformation()
                    {
                        Id = guidMaster,
                        Name = "Test_Master",
                        Type = InstanceType.Master,
                    },
                    new InstanceInformation()
                    {
                        Id = guidSlave,
                        Name = "Test_Slave",
                        Type = InstanceType.Slave,
                    },
                ]));

            var result = await clusterInformationProvider.IsClusterHealthy();

            result.Should().BeFalse();
        }
    }

    public class Consume
    {
        [Fact]
        public async Task Adds_new_instance_on_instance_created_event()
        {
            var master = new InstanceInformation()
            {
                Id = Guid.NewGuid(),
                Name = "Test_Master",
                Type = InstanceType.Master,
            };
            var slave = new InstanceInformation()
            {
                Id = Guid.NewGuid(),
                Name = "Test_Slave",
                Type = InstanceType.Slave,
            };

            var called = false;
            var mediator = Substitute.For<IUiMediator>();
            using var clusterInformationProvider = new ClientClusterInformationProvider(mediator);

            mediator.Request<GetInstances, GetInstancesResponse>(Arg.Any<GetInstances>(), Arg.Any<CancellationToken>())
                .Returns(new GetInstancesResponse([master,]));

            mediator.Request<GetInstances, GetInstancesResponse>(Arg.Is((GetInstances r) => r.InstanceId == slave.Id), Arg.Any<CancellationToken>())
                .Returns(new GetInstancesResponse([slave,]));

            clusterInformationProvider.NewInstanceAdded += (_) => { called = true; return Task.CompletedTask; };

            await clusterInformationProvider.GetInstancesInCluster(CancellationToken.None);

            await clusterInformationProvider.Consume(new ClientContext<InstanceCreated>(new(slave.Id), null), CancellationToken.None);

            var result = await clusterInformationProvider.GetInstancesInCluster(CancellationToken.None);

            result.Count.Should().Be(2);
            result.Should().Contain(master);
            result.Should().Contain(slave);
            called.Should().BeTrue();
        }

        [Fact]
        public async Task Does_nothing_when_instance_already_exists_on_instance_created_event()
        {
            var guid = Guid.NewGuid();
            var called = false;
            var mediator = Substitute.For<IUiMediator>();
            using var clusterInformationProvider = new ClientClusterInformationProvider(mediator);

            mediator.Request<GetInstances, GetInstancesResponse>(Arg.Any<GetInstances>(), Arg.Any<CancellationToken>())
                .Returns(new GetInstancesResponse(
                [
                    new InstanceInformation()
                    {
                        Id = guid,
                        Name = "Test_Master",
                        Type = InstanceType.Master,
                    },
                ]));

            clusterInformationProvider.NewInstanceAdded += (_) => { called = true; return Task.CompletedTask; };

            await clusterInformationProvider.GetInstancesInCluster(CancellationToken.None);

            await clusterInformationProvider.Consume(new ClientContext<InstanceCreated>(new(guid), null), CancellationToken.None);

            var result = await clusterInformationProvider.GetInstancesInCluster(CancellationToken.None);

            var instance = result.Single();
            instance.Id.Should().Be(guid);
            instance.Name.Should().Be("Test_Master");
            instance.Type.Should().Be(InstanceType.Master);
            called.Should().BeFalse();
        }

        [Fact]
        public async Task Does_nothing_when_provider_is_not_initialized_on_instance_created_event()
        {
            var guid = Guid.NewGuid();
            var called = false;
            var mediator = Substitute.For<IUiMediator>();
            using var clusterInformationProvider = new ClientClusterInformationProvider(mediator);

            mediator.Request<GetInstances, GetInstancesResponse>(Arg.Any<GetInstances>(), Arg.Any<CancellationToken>())
                .Returns(new GetInstancesResponse(
                [
                    new InstanceInformation()
                    {
                        Id = guid,
                        Name = "Test_Master",
                        Type = InstanceType.Master,
                    },
                ]));

            clusterInformationProvider.NewInstanceAdded += (_) => { called = true; return Task.CompletedTask; };

            await clusterInformationProvider.Consume(new ClientContext<InstanceCreated>(new(guid), null), CancellationToken.None);

            called.Should().BeFalse();
        }

        [Fact]
        public async Task Changes_health_status_on_instance_health_info_event()
        {
            var guid = Guid.NewGuid();
            var called = false;
            var mediator = Substitute.For<IUiMediator>();
            using var clusterInformationProvider = new ClientClusterInformationProvider(mediator);

            mediator.Request<GetInstances, GetInstancesResponse>(Arg.Any<GetInstances>(), Arg.Any<CancellationToken>())
                .Returns(new GetInstancesResponse(
                [
                    new InstanceInformation()
                    {
                        Id = guid,
                        Name = "Test_Master",
                        Type = InstanceType.Master,
                    },
                ]));

            clusterInformationProvider.HealthStatusChanged += (_, _, _) => { called = true; return Task.CompletedTask; };

            await clusterInformationProvider.GetInstancesInCluster(CancellationToken.None);

            await clusterInformationProvider.Consume(
                new ClientContext<InstanceHealthInfo>(
                    new(guid, DateTime.Now, HealthStatus.Healthy),
                    null),
                CancellationToken.None);

            var result = await clusterInformationProvider.GetHealthStatus(guid);

            result.Should().Be(HealthStatus.Healthy);
            called.Should().BeTrue();
        }

        [Fact]
        public async Task Does_nothing_when_instance_does_not_exist_on_instance_health_info_event()
        {
            var called = false;
            var mediator = Substitute.For<IUiMediator>();
            using var clusterInformationProvider = new ClientClusterInformationProvider(mediator);

            mediator.Request<GetInstances, GetInstancesResponse>(Arg.Any<GetInstances>(), Arg.Any<CancellationToken>())
                .Returns(new GetInstancesResponse(
                [
                    new InstanceInformation()
                    {
                        Id = Guid.NewGuid(),
                        Name = "Test_Master",
                        Type = InstanceType.Master,
                    },
                ]));

            clusterInformationProvider.HealthStatusChanged += (_, _, _) => { called = true; return Task.CompletedTask; };

            await clusterInformationProvider.GetInstancesInCluster(CancellationToken.None);

            await clusterInformationProvider.Consume(
                new ClientContext<InstanceHealthInfo>(
                    new(Guid.NewGuid(), DateTime.Now, HealthStatus.Healthy),
                    null),
                CancellationToken.None);

            called.Should().BeFalse();
        }

        [Fact]
        public async Task Does_nothing_when_provider_is_not_initialized_on_instance_health_info_event()
        {
            var called = false;
            var mediator = Substitute.For<IUiMediator>();
            using var clusterInformationProvider = new ClientClusterInformationProvider(mediator);

            mediator.Request<GetInstances, GetInstancesResponse>(Arg.Any<GetInstances>(), Arg.Any<CancellationToken>())
                .Returns(new GetInstancesResponse(
                [
                    new InstanceInformation()
                    {
                        Id = Guid.NewGuid(),
                        Name = "Test_Master",
                        Type = InstanceType.Master,
                    },
                ]));

            clusterInformationProvider.HealthStatusChanged += (_, _, _) => { called = true; return Task.CompletedTask; };

            await clusterInformationProvider.Consume(
                new ClientContext<InstanceHealthInfo>(
                    new(Guid.NewGuid(), DateTime.Now, HealthStatus.Healthy),
                    null),
                CancellationToken.None);

            called.Should().BeFalse();
        }

        [Fact]
        public async Task Removes_instance_on_instance_administrated_event()
        {
            var guid = Guid.NewGuid();
            var called = false;
            var mediator = Substitute.For<IUiMediator>();
            using var clusterInformationProvider = new ClientClusterInformationProvider(mediator);

            mediator.Request<GetInstances, GetInstancesResponse>(Arg.Any<GetInstances>(), Arg.Any<CancellationToken>())
                .Returns(new GetInstancesResponse(
                [
                    new InstanceInformation()
                    {
                        Id = guid,
                        Name = "Test_Master",
                        Type = InstanceType.Master,
                    },
                ]));

            clusterInformationProvider.InstanceDeleted += (_) => { called = true; return Task.CompletedTask; };

            await clusterInformationProvider.GetInstancesInCluster(CancellationToken.None);

            await clusterInformationProvider.Consume(
                new ClientContext<InstanceAdministrated>(
                    new(guid, AdministrateInstanceAction.Delete, true),
                    null),
                CancellationToken.None);

            var result = await clusterInformationProvider.GetInstancesInCluster(CancellationToken.None);

            result.Should().BeEmpty();
            called.Should().BeTrue();
        }

        [Fact]
        public async Task Invokes_event_when_delete_instance_failed_on_instance_administrated_event()
        {
            var guid = Guid.NewGuid();
            var called = false;
            var mediator = Substitute.For<IUiMediator>();
            using var clusterInformationProvider = new ClientClusterInformationProvider(mediator);

            mediator.Request<GetInstances, GetInstancesResponse>(Arg.Any<GetInstances>(), Arg.Any<CancellationToken>())
                .Returns(new GetInstancesResponse(
                [
                    new InstanceInformation()
                    {
                        Id = guid,
                        Name = "Test_Master",
                        Type = InstanceType.Master,
                    },
                ]));

            clusterInformationProvider.DeleteInstanceFailed += (_) => { called = true; return Task.CompletedTask; };

            await clusterInformationProvider.GetInstancesInCluster(CancellationToken.None);

            await clusterInformationProvider.Consume(
                new ClientContext<InstanceAdministrated>(
                    new(guid, AdministrateInstanceAction.Delete, false),
                    null),
                CancellationToken.None);

            var result = await clusterInformationProvider.GetInstancesInCluster(CancellationToken.None);

            _ = result.Single();
            called.Should().BeTrue();
        }
    }
}
