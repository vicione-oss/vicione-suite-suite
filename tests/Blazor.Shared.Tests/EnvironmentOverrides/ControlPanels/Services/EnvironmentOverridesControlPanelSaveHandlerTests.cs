using AwesomeAssertions;
using Blazor.Shared.EnvironmentOverrides.ControlPanels.Models;
using Blazor.Shared.EnvironmentOverrides.ControlPanels.Services;
using Core.Shared.EnvironmentOverrides.Commands;
using Core.Shared.EnvironmentOverrides.Events;
using NSubstitute;
using Sdk.Client.ControlPanels.Models;
using Sdk.Client.Infrastructure;
using Sdk.Instance;
using Sdk.Messaging;
using Xunit;
using EnvironmentOverridesControlPanelStrings
    = Blazor.Shared.EnvironmentOverrides.ControlPanels.Localization.EnvironmentOverridesControlPanel;

namespace Blazor.Shared.Tests.EnvironmentOverrides.ControlPanels.Services;

public sealed class EnvironmentOverridesControlPanelSaveHandlerTests
{
    private static readonly Guid _localInstanceId = Guid.NewGuid();

    private static readonly IInstanceInformationProvider _instanceInformationProvider = CreateInstanceInformationProvider();

    private static IInstanceInformationProvider CreateInstanceInformationProvider()
    {
        var localInstance = Substitute.For<IInstanceInformation>();
        localInstance.Id.Returns(_localInstanceId);

        var provider = Substitute.For<IInstanceInformationProvider>();
        provider.Local.Returns(localInstance);

        return provider;
    }

    public sealed class Save
    {
        [Fact]
        public async Task Should_send_overrides_and_succeed_on_changed_event()
        {
            // Arrange
            var mediator = Substitute.For<IUiMediator>();
            var correlationId = Guid.Empty;
            using var saveHandler = new EnvironmentOverridesControlPanelSaveHandler(mediator, _instanceInformationProvider);

            var state = new EnvironmentOverridesControlPanelState
            {
                Entries = [new EnvironmentOverrideEntry { Name = "MY_KEY", Value = "my value" }]
            };

            mediator.When(m => m.Send(Arg.Any<SetEnvironmentOverrides>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>()))
                .Do(async callInfo =>
                {
                    var command = callInfo.Arg<SetEnvironmentOverrides>()!;
                    correlationId = command.CorrelationId;

                    var message = new EnvironmentOverridesChanged(correlationId);
                    var context = new ClientContext<EnvironmentOverridesChanged>(message, Guid.NewGuid());

                    await saveHandler.Consume(context, TestContext.Current.CancellationToken);
                });

            // Act
            var result = await saveHandler.Save(state, TestContext.Current.CancellationToken);

            // Assert
            await mediator.Received().Send(
                Arg.Is<SetEnvironmentOverrides>(c => c!.CorrelationId == correlationId
                    && c.Overrides.Count == 1
                    && c.Overrides.ContainsKey("MY_KEY")
                    && c.Overrides["MY_KEY"] == "my value"),
                _localInstanceId,
                Arg.Any<CancellationToken>());

            result.Should().BeOfType<SaveSuccessResult>();
        }

        [Fact]
        public async Task Should_require_a_restart_after_a_successful_save()
        {
            // Arrange
            var mediator = Substitute.For<IUiMediator>();
            using var saveHandler = new EnvironmentOverridesControlPanelSaveHandler(mediator, _instanceInformationProvider);

            var state = new EnvironmentOverridesControlPanelState
            {
                Entries = [new EnvironmentOverrideEntry { Name = "MY_KEY", Value = "my value" }]
            };

            mediator.When(m => m.Send(Arg.Any<SetEnvironmentOverrides>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>()))
                .Do(async callInfo =>
                {
                    var command = callInfo.Arg<SetEnvironmentOverrides>()!;

                    var message = new EnvironmentOverridesChanged(command.CorrelationId);
                    var context = new ClientContext<EnvironmentOverridesChanged>(message, Guid.NewGuid());

                    await saveHandler.Consume(context, TestContext.Current.CancellationToken);
                });

            // Act
            await saveHandler.Save(state, TestContext.Current.CancellationToken);

            // Assert
            state.RestartRequired.Should().BeTrue();
        }

        [Fact]
        public async Task Should_not_require_a_restart_when_the_save_was_blocked()
        {
            // Arrange
            var mediator = Substitute.For<IUiMediator>();
            using var saveHandler = new EnvironmentOverridesControlPanelSaveHandler(mediator, _instanceInformationProvider);

            var state = new EnvironmentOverridesControlPanelState
            {
                Entries = [new EnvironmentOverrideEntry { Name = "HAS SPACE", Value = "value" }]
            };

            // Act
            var result = await saveHandler.Save(state, TestContext.Current.CancellationToken);

            // Assert
            result.Should().BeOfType<SaveErrorResult>();
            state.RestartRequired.Should().BeFalse();
        }

        [Fact]
        public async Task Should_fail_on_error_event()
        {
            // Arrange
            var mediator = Substitute.For<IUiMediator>();
            var correlationId = Guid.Empty;
            using var saveHandler = new EnvironmentOverridesControlPanelSaveHandler(mediator, _instanceInformationProvider);

            var state = new EnvironmentOverridesControlPanelState
            {
                Entries = [new EnvironmentOverrideEntry { Name = "MY_KEY", Value = "value" }]
            };

            mediator.When(m => m.Send(Arg.Any<SetEnvironmentOverrides>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>()))
                .Do(async callInfo =>
                {
                    var command = callInfo.Arg<SetEnvironmentOverrides>()!;
                    correlationId = command.CorrelationId;

                    var message = new SetEnvironmentOverridesError(correlationId, new ErrorInfo(100, "Failure"));
                    var context = new ClientContext<SetEnvironmentOverridesError>(message, Guid.NewGuid());

                    await saveHandler.Consume(context, TestContext.Current.CancellationToken);
                });

            // Act
            var result = await saveHandler.Save(state, TestContext.Current.CancellationToken);

            // Assert
            result.Should().BeOfType<SaveErrorResult>();
        }

        [Fact]
        public async Task Should_drop_fully_blank_rows_without_sending_command()
        {
            // Arrange
            var mediator = Substitute.For<IUiMediator>();
            using var saveHandler = new EnvironmentOverridesControlPanelSaveHandler(mediator, _instanceInformationProvider);

            SetEnvironmentOverrides? sentCommand = null;

            mediator.When(m => m.Send(Arg.Any<SetEnvironmentOverrides>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>()))
                .Do(async callInfo =>
                {
                    var command = callInfo.Arg<SetEnvironmentOverrides>()!;
                    sentCommand = command;

                    var message = new EnvironmentOverridesChanged(command.CorrelationId);
                    var context = new ClientContext<EnvironmentOverridesChanged>(message, Guid.NewGuid());

                    await saveHandler.Consume(context, TestContext.Current.CancellationToken);
                });

            var state = new EnvironmentOverridesControlPanelState
            {
                Entries = [new EnvironmentOverrideEntry(), new EnvironmentOverrideEntry { Name = "  ", Value = "  " }]
            };

            // Act
            var result = await saveHandler.Save(state, TestContext.Current.CancellationToken);

            // Assert
            result.Should().BeOfType<SaveSuccessResult>();

            sentCommand.Should().NotBeNull();
            sentCommand!.Overrides.Should().BeEmpty();

            state.Entries.Should().BeEmpty();
        }

        [Fact]
        public async Task Should_block_save_when_the_overrides_never_loaded()
        {
            // Arrange
            var mediator = Substitute.For<IUiMediator>();
            using var saveHandler = new EnvironmentOverridesControlPanelSaveHandler(mediator, _instanceInformationProvider);

            var state = new EnvironmentOverridesControlPanelState
            {
                LoadError = "The override file cannot be parsed.",
                Entries = []
            };

            // Act
            var result = await saveHandler.Save(state, TestContext.Current.CancellationToken);

            // Assert
            result.Should().BeOfType<SaveErrorResult>();
            ((SaveErrorResult)result).Message.Should()
                .Be(EnvironmentOverridesControlPanelStrings.SaveBlockedByLoadFailureError);

            await mediator.DidNotReceive().Send(Arg.Any<SetEnvironmentOverrides>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task Should_block_save_on_duplicate_key()
        {
            // Arrange
            var mediator = Substitute.For<IUiMediator>();
            using var saveHandler = new EnvironmentOverridesControlPanelSaveHandler(mediator, _instanceInformationProvider);

            var state = new EnvironmentOverridesControlPanelState
            {
                Entries =
                [
                    new EnvironmentOverrideEntry { Name = "MY_KEY", Value = "one" },
                    new EnvironmentOverrideEntry { Name = "MY_KEY", Value = "two" },
                ]
            };

            // Act
            var result = await saveHandler.Save(state, TestContext.Current.CancellationToken);

            // Assert
            result.Should().BeOfType<SaveErrorResult>();
            ((SaveErrorResult)result).Message.Should().Contain("MY_KEY");

            await mediator.DidNotReceive().Send(Arg.Any<SetEnvironmentOverrides>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>());
        }

        [Theory]
        [InlineData("1_STARTS_WITH_DIGIT")]
        [InlineData("HAS SPACE")]
        [InlineData("HAS-DASH")]
        [InlineData("FOO=\"benign\"\nConnectionStrings__Default")]
        public async Task Should_block_save_on_invalid_key(string invalidKey)
        {
            // Arrange
            var mediator = Substitute.For<IUiMediator>();
            using var saveHandler = new EnvironmentOverridesControlPanelSaveHandler(mediator, _instanceInformationProvider);

            var state = new EnvironmentOverridesControlPanelState
            {
                Entries = [new EnvironmentOverrideEntry { Name = invalidKey, Value = "value" }]
            };

            // Act
            var result = await saveHandler.Save(state, TestContext.Current.CancellationToken);

            // Assert
            result.Should().BeOfType<SaveErrorResult>();

            await mediator.DidNotReceive().Send(Arg.Any<SetEnvironmentOverrides>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task Should_block_save_on_value_containing_nul()
        {
            // Arrange
            var mediator = Substitute.For<IUiMediator>();
            using var saveHandler = new EnvironmentOverridesControlPanelSaveHandler(mediator, _instanceInformationProvider);

            var state = new EnvironmentOverridesControlPanelState
            {
                Entries = [new EnvironmentOverrideEntry { Name = "MY_KEY", Value = "bad\0value" }]
            };

            // Act
            var result = await saveHandler.Save(state, TestContext.Current.CancellationToken);

            // Assert
            result.Should().BeOfType<SaveErrorResult>();
            ((SaveErrorResult)result).Message.Should().Contain("MY_KEY");

            await mediator.DidNotReceive().Send(Arg.Any<SetEnvironmentOverrides>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>());
        }

        [Theory]
        [InlineData("MY_KEY\n")]
        [InlineData("  MY_KEY  ")]
        [InlineData("MY_KEY\t")]
        public async Task Should_send_trimmed_key(string untrimmedKey)
        {
            // Arrange
            var mediator = Substitute.For<IUiMediator>();
            using var saveHandler = new EnvironmentOverridesControlPanelSaveHandler(mediator, _instanceInformationProvider);

            SetEnvironmentOverrides? sentCommand = null;

            mediator.When(m => m.Send(Arg.Any<SetEnvironmentOverrides>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>()))
                .Do(async callInfo =>
                {
                    var command = callInfo.Arg<SetEnvironmentOverrides>()!;
                    sentCommand = command;

                    var message = new EnvironmentOverridesChanged(command.CorrelationId);
                    var context = new ClientContext<EnvironmentOverridesChanged>(message, Guid.NewGuid());

                    await saveHandler.Consume(context, TestContext.Current.CancellationToken);
                });

            var state = new EnvironmentOverridesControlPanelState
            {
                Entries = [new EnvironmentOverrideEntry { Name = untrimmedKey, Value = "value" }]
            };

            // Act
            var result = await saveHandler.Save(state, TestContext.Current.CancellationToken);

            // Assert
            result.Should().BeOfType<SaveSuccessResult>();

            sentCommand.Should().NotBeNull();
            sentCommand!.Overrides.Should().ContainSingle(o => o.Key == "MY_KEY");
        }

        [Fact]
        public async Task Should_block_save_on_value_without_key()
        {
            // Arrange
            var mediator = Substitute.For<IUiMediator>();
            using var saveHandler = new EnvironmentOverridesControlPanelSaveHandler(mediator, _instanceInformationProvider);

            var state = new EnvironmentOverridesControlPanelState
            {
                Entries = [new EnvironmentOverrideEntry { Name = "", Value = "orphan value" }]
            };

            // Act
            var result = await saveHandler.Save(state, TestContext.Current.CancellationToken);

            // Assert
            result.Should().BeOfType<SaveErrorResult>();

            await mediator.DidNotReceive().Send(Arg.Any<SetEnvironmentOverrides>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>());
        }
    }
}
