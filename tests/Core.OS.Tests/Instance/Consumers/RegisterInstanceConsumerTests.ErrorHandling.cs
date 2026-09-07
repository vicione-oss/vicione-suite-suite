using Core.OS.DbContext;
using Core.OS.Instance;
using Core.OS.Instance.Commands;
using Core.OS.Instance.Consumers;
using Core.OS.Instance.Services;
using Core.OS.Persistence;
using Core.Shared.Instance.Requests;
using MassTransit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Sdk.Backend.Messaging;
using Sdk.Backend.Persistence;
using Sdk.Instance;
using Sdk.Testing.Backend;
using Xunit;

namespace Core.OS.Tests.Instance.Consumers;

public partial class RegisterInstanceConsumerTests
{
    /// <summary>
    /// ADR-002: registration is not idempotent by construction — it must never report a partially
    /// applied registration as done. Failures propagate so MassTransit retries and finally
    /// dead-letters the command.
    /// </summary>
    public sealed class ErrorHandling
    {
        [Fact]
        public async Task Should_propagate_exception_when_persistence_is_unavailable()
        {
            // Arrange
            var failingDb = Substitute.For<IApplicationDbContext>();
            failingDb.InstanceInfo.Throws(new InvalidOperationException("database unavailable"));

            var mediator = Substitute.For<ISuiteMediator>();
            mediator.Request<GetInstances, GetInstancesResponse>(Arg.Any<GetInstances>(), Arg.Any<CancellationToken>())
                .Returns(new GetInstancesResponse([]));

            await using var tester = new MassTransitTester(services =>
            {
                services.AddConsumer<RegisterInstanceConsumer>();
                services.AddSingleton(failingDb);
                services.AddSingleton(new ModuleContextTypeInformation(Shared.Constants.SystemModuleId, typeof(ApplicationDbContext), typeof(ApplicationDbContext).AssemblyQualifiedName!));
                services.AddRoutingSlipBuilderFactory();
                services.AddSingleton(Substitute.For<ILocalInstanceInformationProvider>());
                services.AddSingleton(new InMemoryClusterInformationProvider(Substitute.For<ILogger<InMemoryClusterInformationProvider>>()));
                services.AddSingleton(mediator);
                services.AddSingleton(Substitute.For<IInstanceConfigurationRepository>());
                services.AddSingleton(new SynchronizationState());
            });

            var command = new RegisterInstance
            {
                Name = "Test",
                InstanceId = Guid.NewGuid(),
                Type = InstanceType.Standalone
            };

            // Act + Assert
            await Assert.ThrowsAsync<InvalidOperationException>(
                () => tester.TestCommandFault<RegisterInstance, RegisterInstanceConsumer>(command));
        }
    }
}
