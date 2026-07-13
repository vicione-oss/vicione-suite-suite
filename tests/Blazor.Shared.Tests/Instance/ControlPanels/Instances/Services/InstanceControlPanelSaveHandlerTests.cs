using AwesomeAssertions;
using Blazor.Shared.Instance.ControlPanels.Instances.Models;
using Blazor.Shared.Instance.ControlPanels.Instances.Services;
using Blazor.Shared.Services;
using Core.Shared.Instance.Commands;
using Core.Shared.Instance.Contracts;
using Core.Shared.Instance.Events;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Sdk.Client.ControlPanels.Models;
using Sdk.Client.Infrastructure;
using Xunit;

namespace Blazor.Shared.Tests.Instance.ControlPanels.Instances.Services;

public sealed class InstanceControlPanelSaveHandlerTests
{
    public sealed class Save
    {
        [Fact]
        public async Task Should_succeed_saving_instance_information()
        {
            // Arrange
            var clientMediator = Substitute.For<IUiMediator>();
            var correlationId = Guid.Empty;
            using var saveHandler = new InstanceControlPanelSaveHandler(clientMediator, Substitute.For<IBackendLogService>(), Substitute.For<ILogger<InstanceControlPanelSaveHandler>>());

            var state = new InstanceControlPanelState
            {
                InstanceInformation = new InstanceInformationModel(new InstanceInformation())
            };

            clientMediator.When(m => m.Send(Arg.Any<UpdateInstanceInformation>(), Arg.Any<CancellationToken>()))
                .Do(async callinfo =>
                {
                    var command = callinfo.Arg<UpdateInstanceInformation>();
                    correlationId = command.CorrelationId;
                    var information = command.InstanceInformation;

                    var message = new InstanceInformationUpdated(correlationId, information);
                    var context = new ClientContext<InstanceInformationUpdated>(message, Guid.NewGuid());

                    await saveHandler.Consume(context, TestContext.Current.CancellationToken);
                });

            // Act
            var handlerResult = await saveHandler.Save(state, TestContext.Current.CancellationToken);

            // Assert
            await clientMediator.Received().Send(Arg.Is<UpdateInstanceInformation>(a => a.CorrelationId == correlationId), Arg.Any<CancellationToken>());

            handlerResult.Should().BeOfType<SaveSuccessResult>();
        }

        [Fact]
        public async Task Should_fail_saving_instance_information()
        {
            // Arrange
            var clientMediator = Substitute.For<IUiMediator>();
            var correlationId = Guid.Empty;
            using var saveHandler = new InstanceControlPanelSaveHandler(clientMediator, Substitute.For<IBackendLogService>(), Substitute.For<ILogger<InstanceControlPanelSaveHandler>>());

            var state = new InstanceControlPanelState
            {
                InstanceInformation = new InstanceInformationModel(new InstanceInformation())
            };

            clientMediator.When(m => m.Send(Arg.Any<UpdateInstanceInformation>(), Arg.Any<CancellationToken>()))
                .Do(async callinfo =>
                {
                    var command = callinfo.Arg<UpdateInstanceInformation>();
                    correlationId = command.CorrelationId;
                    var information = command.InstanceInformation;

                    var message = new InstanceInformationUpdated(correlationId, information, new Sdk.Messaging.ErrorInfo(100, "Failure"));
                    var context = new ClientContext<InstanceInformationUpdated>(message, Guid.NewGuid());

                    await saveHandler.Consume(context, TestContext.Current.CancellationToken);
                });

            // Act
            var handlerResult = await saveHandler.Save(state, TestContext.Current.CancellationToken);

            // Assert
            await clientMediator.Received().Send(Arg.Is<UpdateInstanceInformation>(a => a.CorrelationId == correlationId), Arg.Any<CancellationToken>());

            handlerResult.Should().BeOfType<SaveErrorResult>();
        }
    }
}
