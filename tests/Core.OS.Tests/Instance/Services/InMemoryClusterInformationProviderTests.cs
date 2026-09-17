using Core.OS.Instance.Services;
using Core.Shared.Instance.Contracts;
using Core.Shared.Instance.Requests;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using Sdk.Backend.Messaging;
using Sdk.Instance;

namespace Core.OS.Tests.Instance.Services;

public sealed class InMemoryClusterInformationProviderTests
{
    private static InMemoryClusterInformationProvider InitProvider()
        => new(Substitute.For<ILogger<InMemoryClusterInformationProvider>>());

    public sealed class Initialize
    {
        [Fact]
        public async Task Should_add_instances()
        {
            var guid = Guid.NewGuid();
            var mediator = Substitute.For<ISuiteMediator>();
            var clusterInformationProvider = InitProvider();

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

            await clusterInformationProvider.Initialize(mediator, TestContext.Current.CancellationToken);

            var result = await clusterInformationProvider.GetInstancesInCluster(TestContext.Current.CancellationToken);

            var instance = result.Single();
            instance.Id.Should().Be(guid);
            instance.Name.Should().Be("Test");
            instance.Type.Should().Be(InstanceType.Master);
        }
    }

    public sealed class GetHealthStatus
    {
        [Fact]
        public async Task Should_return_correct_health_status()
        {
            var guid = Guid.NewGuid();
            var mediator = Substitute.For<ISuiteMediator>();
            var clusterInformationProvider = InitProvider();

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

            await clusterInformationProvider.Initialize(mediator, TestContext.Current.CancellationToken);

            var result = await clusterInformationProvider.GetHealthStatus(guid, TestContext.Current.CancellationToken);

            result.Should().BeNull();
        }
    }

    public sealed class GetInstancesInCluster
    {
        [Fact]
        public async Task Should_return_correct_list()
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

            var mediator = Substitute.For<ISuiteMediator>();
            var clusterInformationProvider = InitProvider();

            mediator.Request<GetInstances, GetInstancesResponse>(Arg.Any<GetInstances>(), Arg.Any<CancellationToken>())
                .Returns(new GetInstancesResponse([master, slave,]));

            await clusterInformationProvider.Initialize(mediator, TestContext.Current.CancellationToken);

            var result = await clusterInformationProvider.GetInstancesInCluster(TestContext.Current.CancellationToken);

            result.Count.Should().Be(2);
            result.Should().Contain(master);
            result.Should().Contain(slave);
        }

        [Fact]
        public async Task Should_return_empty_list()
        {
            var clusterInformationProvider = InitProvider();

            var result = await clusterInformationProvider.GetInstancesInCluster(TestContext.Current.CancellationToken);

            result.Should().BeEmpty();
        }
    }

    public sealed class IsClusterHealthy
    {
        [Theory]
        [InlineData(HealthStatus.Healthy, HealthStatus.Healthy, true)]
        [InlineData(HealthStatus.Healthy, HealthStatus.Unhealthy, false)]
        [InlineData(HealthStatus.Unhealthy, HealthStatus.Unhealthy, false)]
        [InlineData(HealthStatus.Healthy, null, false)]
        [InlineData(null, null, false)]
        public async Task Should_return_correct_value(HealthStatus? masterHealthStatus, HealthStatus? slaveHealthStatus, bool expected)
        {
            var guidMaster = Guid.NewGuid();
            var guidSlave = Guid.NewGuid();
            var mediator = Substitute.For<ISuiteMediator>();
            var clusterInformationProvider = InitProvider();

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

            await clusterInformationProvider.Initialize(mediator, TestContext.Current.CancellationToken);

            if (masterHealthStatus is not null)
                await clusterInformationProvider.ChangeHealthInfo(guidMaster, (HealthStatus)masterHealthStatus, DateTimeOffset.Now);

            if (slaveHealthStatus is not null)
                await clusterInformationProvider.ChangeHealthInfo(guidSlave, (HealthStatus)slaveHealthStatus, DateTimeOffset.Now);

            var result = await clusterInformationProvider.IsClusterHealthy(TestContext.Current.CancellationToken);

            result.Should().Be(expected);
        }
    }

    public sealed class AddNewInstance
    {
        [Fact]
        public async Task Should_add_new_instance()
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
            var mediator = Substitute.For<ISuiteMediator>();
            var clusterInformationProvider = InitProvider();

            mediator.Request<GetInstances, GetInstancesResponse>(Arg.Any<GetInstances>(), Arg.Any<CancellationToken>())
                .Returns(new GetInstancesResponse([master,]));

            mediator.Request<GetInstances, GetInstancesResponse>(Arg.Is((GetInstances r) => r.InstanceId == slave.Id), Arg.Any<CancellationToken>())
                .Returns(new GetInstancesResponse([slave,]));

            clusterInformationProvider.NewInstanceAdded += (_) => { called = true; return Task.CompletedTask; };

            await clusterInformationProvider.Initialize(mediator, TestContext.Current.CancellationToken);

            await clusterInformationProvider.AddNewInstance(slave);

            var result = await clusterInformationProvider.GetInstancesInCluster(TestContext.Current.CancellationToken);

            result.Count.Should().Be(2);
            result.Should().Contain(master);
            result.Should().Contain(slave);
            called.Should().BeTrue();
        }

        [Fact]
        public async Task Should_do_nothing_when_instance_already_exists()
        {
            var master = new InstanceInformation()
            {
                Id = Guid.NewGuid(),
                Name = "Test_Master",
                Type = InstanceType.Master,
            };
            var called = false;
            var mediator = Substitute.For<ISuiteMediator>();
            var clusterInformationProvider = InitProvider();

            mediator.Request<GetInstances, GetInstancesResponse>(Arg.Any<GetInstances>(), Arg.Any<CancellationToken>())
                .Returns(new GetInstancesResponse([master,]));

            clusterInformationProvider.NewInstanceAdded += (_) => { called = true; return Task.CompletedTask; };

            await clusterInformationProvider.Initialize(mediator, TestContext.Current.CancellationToken);

            await clusterInformationProvider.AddNewInstance(master);

            var result = await clusterInformationProvider.GetInstancesInCluster(TestContext.Current.CancellationToken);

            var instance = result.Single();
            instance.Should().Be(master);
            called.Should().BeFalse();
        }
    }

    public sealed class ChangeHealthInfo
    {
        [Fact]
        public async Task Should_change_health_status()
        {
            var guid = Guid.NewGuid();
            var called = false;
            var mediator = Substitute.For<ISuiteMediator>();
            var clusterInformationProvider = InitProvider();

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

            await clusterInformationProvider.Initialize(mediator, TestContext.Current.CancellationToken);

            await clusterInformationProvider.ChangeHealthInfo(guid, HealthStatus.Healthy, DateTimeOffset.Now);

            var result = await clusterInformationProvider.GetHealthStatus(guid, TestContext.Current.CancellationToken);

            result.Should().Be(HealthStatus.Healthy);
            called.Should().BeTrue();
        }

        [Fact]
        public async Task Should_do_nothing_when_instance_does_not_exist()
        {
            var called = false;
            var mediator = Substitute.For<ISuiteMediator>();
            var clusterInformationProvider = InitProvider();

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

            await clusterInformationProvider.Initialize(mediator, TestContext.Current.CancellationToken);

            await clusterInformationProvider.ChangeHealthInfo(Guid.NewGuid(), HealthStatus.Healthy, DateTimeOffset.Now);

            called.Should().BeFalse();
        }
    }

    public class RemoveInstance
    {
        [Fact]
        public async Task Removes_instance()
        {
            var guid = Guid.NewGuid();
            var called = false;
            var mediator = Substitute.For<ISuiteMediator>();
            var clusterInformationProvider = InitProvider();

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

            await clusterInformationProvider.Initialize(mediator, TestContext.Current.CancellationToken);

            await clusterInformationProvider.RemoveInstance(guid, true);

            var result = await clusterInformationProvider.GetInstancesInCluster(TestContext.Current.CancellationToken);

            result.Should().BeEmpty();
            called.Should().BeTrue();
        }

        [Fact]
        public async Task Invokes_event_when_delete_instance_failed()
        {
            var guid = Guid.NewGuid();
            var called = false;
            var mediator = Substitute.For<ISuiteMediator>();
            var clusterInformationProvider = InitProvider();

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

            await clusterInformationProvider.Initialize(mediator, TestContext.Current.CancellationToken);

            await clusterInformationProvider.RemoveInstance(guid, false);

            var result = await clusterInformationProvider.GetInstancesInCluster(TestContext.Current.CancellationToken);

            _ = result.Single();
            called.Should().BeTrue();
        }
    }
}
