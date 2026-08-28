using AwesomeAssertions;
using Blazor.Shared.EnvironmentOverrides.ControlPanels.Services;
using Core.Shared.EnvironmentOverrides.Requests;
using Microsoft.Extensions.Logging;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Sdk.Client.Infrastructure;
using Sdk.Instance;
using Sdk.Messaging;
using ViciOne.Ui.Localization.Resources;
using Xunit;

namespace Blazor.Shared.Tests.EnvironmentOverrides.ControlPanels.Services;

public sealed class EnvironmentOverridesControlPanelResetHandlerTests
{
    private static readonly ILogger<EnvironmentOverridesControlPanelResetHandler> _logger =
        Substitute.For<ILogger<EnvironmentOverridesControlPanelResetHandler>>();

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

    [Fact]
    public async Task Should_read_the_overrides_of_the_local_instance()
    {
        // Arrange
        var mediator = Substitute.For<IUiMediator>();
        mediator.Request<GetEnvironmentOverrides, GetEnvironmentOverridesResponse>(Arg.Any<GetEnvironmentOverrides>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(new GetEnvironmentOverridesResponse(new Dictionary<string, string>()));

        var resetHandler = new EnvironmentOverridesControlPanelResetHandler(mediator, _instanceInformationProvider, _logger);
        var state = new EnvironmentOverridesControlPanelState();

        // Act
        await resetHandler.Reset(state, TestContext.Current.CancellationToken);

        // Assert
        await mediator.Received().Request<GetEnvironmentOverrides, GetEnvironmentOverridesResponse>(
            Arg.Any<GetEnvironmentOverrides>(),
            _localInstanceId,
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Should_clear_a_pending_restart_requirement()
    {
        // Arrange
        var mediator = Substitute.For<IUiMediator>();
        mediator.Request<GetEnvironmentOverrides, GetEnvironmentOverridesResponse>(Arg.Any<GetEnvironmentOverrides>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(new GetEnvironmentOverridesResponse(new Dictionary<string, string> { ["MY_KEY"] = "value" }));

        var resetHandler = new EnvironmentOverridesControlPanelResetHandler(mediator, _instanceInformationProvider, _logger);
        var state = new EnvironmentOverridesControlPanelState { RestartRequired = true };

        // Act
        await resetHandler.Reset(state, TestContext.Current.CancellationToken);

        // Assert
        state.RestartRequired.Should().BeFalse();
    }

    [Fact]
    public async Task Should_populate_state_from_query_response()
    {
        // Arrange
        var mediator = Substitute.For<IUiMediator>();
        mediator.Request<GetEnvironmentOverrides, GetEnvironmentOverridesResponse>(Arg.Any<GetEnvironmentOverrides>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(new GetEnvironmentOverridesResponse(
                new Dictionary<string, string> { ["OTEL_EXPORTER_OTLP_ENDPOINT"] = "http://collector:4317" }));

        var resetHandler = new EnvironmentOverridesControlPanelResetHandler(mediator, _instanceInformationProvider, _logger);
        var state = new EnvironmentOverridesControlPanelState();

        // Act
        await resetHandler.Reset(state, TestContext.Current.CancellationToken);

        // Assert
        state.Entries.Should().ContainSingle(e => e.Name == "OTEL_EXPORTER_OTLP_ENDPOINT" && e.Value == "http://collector:4317");
        state.IsLoading.Should().BeFalse();
    }

    [Fact]
    public async Task Should_leave_the_entries_empty_when_no_overrides_exist()
    {
        // Arrange
        var mediator = Substitute.For<IUiMediator>();
        mediator.Request<GetEnvironmentOverrides, GetEnvironmentOverridesResponse>(Arg.Any<GetEnvironmentOverrides>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(new GetEnvironmentOverridesResponse(new Dictionary<string, string>()));

        var resetHandler = new EnvironmentOverridesControlPanelResetHandler(mediator, _instanceInformationProvider, _logger);
        var state = new EnvironmentOverridesControlPanelState();

        // Act
        await resetHandler.Reset(state, TestContext.Current.CancellationToken);

        // Assert
        state.Entries.Should().BeEmpty();
    }

    [Fact]
    public async Task Should_report_request_error_without_initializing_entries()
    {
        // Arrange
        var mediator = Substitute.For<IUiMediator>();
        mediator.Request<GetEnvironmentOverrides, GetEnvironmentOverridesResponse>(Arg.Any<GetEnvironmentOverrides>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(new GetEnvironmentOverridesResponse(new Dictionary<string, string>(),
                new ErrorInfo(0, "The override file cannot be parsed.")));

        var resetHandler = new EnvironmentOverridesControlPanelResetHandler(mediator, _instanceInformationProvider, _logger);
        var state = new EnvironmentOverridesControlPanelState();

        // Act
        await resetHandler.Reset(state, TestContext.Current.CancellationToken);

        // Assert
        state.LoadError.Should().Be("The override file cannot be parsed.");
        state.Entries.Should().BeEmpty();
        state.IsLoading.Should().BeFalse();
    }

    [Fact]
    public async Task Should_report_generic_message_when_request_error_carries_none()
    {
        // Arrange
        var mediator = Substitute.For<IUiMediator>();
        mediator.Request<GetEnvironmentOverrides, GetEnvironmentOverridesResponse>(Arg.Any<GetEnvironmentOverrides>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(new GetEnvironmentOverridesResponse(new Dictionary<string, string>(), new ErrorInfo(0, null)));

        var resetHandler = new EnvironmentOverridesControlPanelResetHandler(mediator, _instanceInformationProvider, _logger);
        var state = new EnvironmentOverridesControlPanelState();

        // Act
        await resetHandler.Reset(state, TestContext.Current.CancellationToken);

        // Assert
        state.LoadError.Should().Be(CommonPhrases.AnUnknownErrorOccurred);
        state.Entries.Should().BeEmpty();
    }

    [Fact]
    public async Task Should_clear_previous_load_error_on_successful_load()
    {
        // Arrange
        var mediator = Substitute.For<IUiMediator>();
        mediator.Request<GetEnvironmentOverrides, GetEnvironmentOverridesResponse>(Arg.Any<GetEnvironmentOverrides>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(new GetEnvironmentOverridesResponse(new Dictionary<string, string> { ["MY_KEY"] = "value" }));

        var resetHandler = new EnvironmentOverridesControlPanelResetHandler(mediator, _instanceInformationProvider, _logger);
        var state = new EnvironmentOverridesControlPanelState { LoadError = "previous failure" };

        // Act
        await resetHandler.Reset(state, TestContext.Current.CancellationToken);

        // Assert
        state.LoadError.Should().BeNull();
        state.Entries.Should().ContainSingle(e => e.Name == "MY_KEY");
    }

    [Fact]
    public async Task Should_report_request_exception_without_initializing_entries()
    {
        // Arrange
        var mediator = Substitute.For<IUiMediator>();
        mediator.Request<GetEnvironmentOverrides, GetEnvironmentOverridesResponse>(Arg.Any<GetEnvironmentOverrides>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("no connection"));

        var resetHandler = new EnvironmentOverridesControlPanelResetHandler(mediator, _instanceInformationProvider, _logger);
        var state = new EnvironmentOverridesControlPanelState();

        // Act
        await resetHandler.Reset(state, TestContext.Current.CancellationToken);

        // Assert
        state.LoadError.Should().Be(CommonPhrases.AnUnknownErrorOccurred);
        state.Entries.Should().BeEmpty();
        state.IsLoading.Should().BeFalse();
    }
}
