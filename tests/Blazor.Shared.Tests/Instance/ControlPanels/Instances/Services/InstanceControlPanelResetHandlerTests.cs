using Blazor.Shared.Instance.ControlPanels.Instances.Services;
using Blazor.Shared.Services;
using Core.Shared.Instance.Contracts;
using Core.Shared.Instance.Requests;
using Core.Shared.Mail;
using Core.Shared.Mail.Contracts;
using Core.Shared.Mail.Requests;
using Microsoft.Extensions.Logging;
using Sdk.Client.Infrastructure;
using Sdk.Messaging;

namespace Blazor.Shared.Tests.Instance.ControlPanels.Instances.Services;

public sealed class InstanceControlPanelResetHandlerTests : IDisposable
{
    private static readonly SmtpSettings Settings = new("mail.corp.example", 587, "vicione-master", "noreply@corp.example", "ViciOne Suite", PasswordSet: true, MailSendingEnabled: true);

    private readonly IUiMediator _mediator = Substitute.For<IUiMediator>();
    private readonly InstanceControlPanelResetHandler _resetHandler;

    public InstanceControlPanelResetHandlerTests()
    {
        _mediator.Request<GetInstances, GetInstancesResponse>(Arg.Any<GetInstances>(), Arg.Any<CancellationToken>())
            .Returns(new GetInstancesResponse([new InstanceInformation()]));

        _resetHandler = new InstanceControlPanelResetHandler(_mediator, Substitute.For<IBackendLogService>(),
            Substitute.For<ILogger<InstanceControlPanelResetHandler>>());
    }

    public void Dispose() => _resetHandler.Dispose();

    private void SetupSmtpSettingsResponse(Task<GetSmtpSettingsResponse> response)
        => _mediator.Request<GetSmtpSettings, GetSmtpSettingsResponse>(Arg.Any<GetSmtpSettings>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(response);

    [Fact]
    public async Task Should_request_the_smtp_settings_of_the_selected_instance()
    {
        // Arrange
        var instanceId = Guid.NewGuid();
        SetupSmtpSettingsResponse(Task.FromResult(new GetSmtpSettingsResponse(Settings)));

        var state = new InstanceControlPanelState { InstanceId = instanceId };

        // Act
        await _resetHandler.Reset(state, TestContext.Current.CancellationToken);

        // Assert
        await _mediator.Received(1).Request<GetSmtpSettings, GetSmtpSettingsResponse>(Arg.Any<GetSmtpSettings>(), instanceId, Arg.Any<CancellationToken>());
        state.SmtpSettings.Should().Be(Settings);
        state.SmtpSettingsLoading.Should().BeFalse();
        state.SmtpSettingsLoadFailed.Should().BeFalse();
    }

    [Fact]
    public async Task Should_finish_loading_the_instance_information_while_the_smtp_settings_are_still_pending()
    {
        // Arrange
        SetupSmtpSettingsResponse(new TaskCompletionSource<GetSmtpSettingsResponse>().Task);

        var state = new InstanceControlPanelState { InstanceId = Guid.NewGuid() };

        // Act
        await _resetHandler.Reset(state, TestContext.Current.CancellationToken);

        // Assert
        state.IsLoading.Should().BeFalse();
        state.InstanceInformation.Should().NotBeNull();
        state.SmtpSettingsLoading.Should().BeTrue();
    }

    [Fact]
    public async Task Should_mark_the_smtp_settings_as_failed_when_the_instance_answers_with_an_error()
    {
        // Arrange
        SetupSmtpSettingsResponse(Task.FromResult(new GetSmtpSettingsResponse(null, new ErrorInfo(MailErrorCodes.GetSmtpSettingsFailed, "Configuration unreadable"))));

        var state = new InstanceControlPanelState { InstanceId = Guid.NewGuid() };

        // Act
        await _resetHandler.Reset(state, TestContext.Current.CancellationToken);

        // Assert
        state.SmtpSettingsLoadFailed.Should().BeTrue();
        state.SmtpSettingsLoading.Should().BeFalse();
    }

    [Fact]
    public async Task Should_mark_the_smtp_settings_as_failed_when_the_request_throws()
    {
        // Arrange
        SetupSmtpSettingsResponse(Task.FromException<GetSmtpSettingsResponse>(new TimeoutException("Request timed out")));

        var state = new InstanceControlPanelState { InstanceId = Guid.NewGuid() };

        // Act
        await _resetHandler.Reset(state, TestContext.Current.CancellationToken);

        // Assert
        state.SmtpSettingsLoadFailed.Should().BeTrue();
        state.SmtpSettingsLoading.Should().BeFalse();
    }

    [Fact]
    public async Task Should_cancel_the_smtp_settings_request_of_a_superseded_load()
    {
        // Arrange
        var requestTokens = new List<CancellationToken>();
        _mediator.Request<GetSmtpSettings, GetSmtpSettingsResponse>(Arg.Any<GetSmtpSettings>(), Arg.Any<Guid>(), Arg.Do<CancellationToken>(requestTokens.Add))
            .Returns(new TaskCompletionSource<GetSmtpSettingsResponse>().Task);

        var state = new InstanceControlPanelState { InstanceId = Guid.NewGuid() };
        await _resetHandler.Reset(state, TestContext.Current.CancellationToken);

        // Act
        await _resetHandler.Reset(state, TestContext.Current.CancellationToken);

        // Assert
        requestTokens.Select(t => t.IsCancellationRequested).Should().Equal(true, false);
        state.SmtpSettingsLoading.Should().BeTrue();
        state.SmtpSettingsLoadFailed.Should().BeFalse();
    }

    [Fact]
    public async Task Should_cancel_a_pending_smtp_settings_request_when_disposed()
    {
        // Arrange
        var requestToken = CancellationToken.None;
        _mediator.Request<GetSmtpSettings, GetSmtpSettingsResponse>(Arg.Any<GetSmtpSettings>(), Arg.Any<Guid>(), Arg.Do<CancellationToken>(t => requestToken = t))
            .Returns(new TaskCompletionSource<GetSmtpSettingsResponse>().Task);

        await _resetHandler.Reset(new InstanceControlPanelState { InstanceId = Guid.NewGuid() }, TestContext.Current.CancellationToken);

        // Act
        _resetHandler.Dispose();

        // Assert
        requestToken.IsCancellationRequested.Should().BeTrue();
    }

    [Fact]
    public async Task Should_discard_a_response_of_a_superseded_load_that_arrives_after_the_cancellation()
    {
        // Arrange
        var supersededResponse = new TaskCompletionSource<GetSmtpSettingsResponse>();
        var latestResponse = new TaskCompletionSource<GetSmtpSettingsResponse>();
        var latestSettings = Settings with { ServerAddress = "smtp.plant-b.local" };

        var state = new InstanceControlPanelState { InstanceId = Guid.NewGuid() };

        SetupSmtpSettingsResponse(supersededResponse.Task);
        await _resetHandler.Reset(state, TestContext.Current.CancellationToken);

        SetupSmtpSettingsResponse(latestResponse.Task);
        await _resetHandler.Reset(state, TestContext.Current.CancellationToken);

        latestResponse.SetResult(new GetSmtpSettingsResponse(latestSettings));

        // Act
        supersededResponse.SetResult(new GetSmtpSettingsResponse(Settings));

        // Assert
        state.SmtpSettings.Should().Be(latestSettings);
    }

    [Fact]
    public async Task Should_discard_a_failure_of_a_superseded_load_that_arrives_after_the_cancellation()
    {
        // Arrange
        var supersededResponse = new TaskCompletionSource<GetSmtpSettingsResponse>();
        var latestResponse = new TaskCompletionSource<GetSmtpSettingsResponse>();

        var state = new InstanceControlPanelState { InstanceId = Guid.NewGuid() };

        SetupSmtpSettingsResponse(supersededResponse.Task);
        await _resetHandler.Reset(state, TestContext.Current.CancellationToken);

        SetupSmtpSettingsResponse(latestResponse.Task);
        await _resetHandler.Reset(state, TestContext.Current.CancellationToken);

        latestResponse.SetResult(new GetSmtpSettingsResponse(Settings));

        // Act
        supersededResponse.SetException(new TimeoutException("Request timed out"));

        // Assert
        state.SmtpSettings.Should().Be(Settings);
        state.SmtpSettingsLoadFailed.Should().BeFalse();
    }
}
