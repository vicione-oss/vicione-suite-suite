using Blazor.Shared.Onboarding.Models;
using Blazor.Shared.Onboarding.Services;
using Blazor.Shared.UserManagement.Contracts;
using Blazor.Shared.UserManagement.Services;
using Core.Shared.HostManagement.Commands;
using Core.Shared.HostManagement.Events;
using Core.Shared.Instance.Commands;
using Core.Shared.Instance.Contracts;
using Core.Shared.Instance.Events;
using Core.Shared.Instance.Services;
using Core.Shared.UserManagement.Configuration;
using Core.Shared.UserManagement.Contracts;
using Microsoft.Extensions.Logging.Abstractions;
using Sdk.Client.Infrastructure;
using Sdk.Client.Wizards.Models;
using Sdk.Instance;
using Sdk.Messaging;
using Sdk.SystemConfiguration.Events;
using Sdk.Testing.Client;

namespace Blazor.Shared.Tests.Onboarding.Services;

public sealed class SummaryWizardPageSaveHandlerTests : IDisposable
{
    private const string AdministratorName = "admin";
    private const string InitialPassword = "initial-password";
    private const string NewPassword = "new-password-123";

    private static readonly TimeZoneInfo s_timeZone = TimeZoneInfo.FindSystemTimeZoneById("Europe/Berlin");

    private readonly ITargetConfigurationProvider _targetConfigurationProvider = Substitute.For<ITargetConfigurationProvider>();
    private readonly IUserService _userService = Substitute.For<IUserService>();
    private readonly IAdministratorInitialPasswordProvider _initialPasswordProvider = Substitute.For<IAdministratorInitialPasswordProvider>();
    private readonly IUiMediator _mediator = Substitute.For<IUiMediator>();
    private readonly IOnboardingStateStore _onboardingStateStore = Substitute.For<IOnboardingStateStore>();
    private readonly IInstanceInformationProvider _instanceInformationProvider = Substitute.For<IInstanceInformationProvider>();
    private readonly TargetConfiguration _targetConfiguration = new()
    {
        UserCredentials = new UserCredentials(AdministratorName, NewPassword),
        TimeZone = s_timeZone
    };
    private readonly UserProfile _administrator = new() { UserName = new UserName(AdministratorName) };
    private readonly List<bool> _completedWrites = [];
    private readonly SummaryWizardPageSaveHandler _handler;

    private Action<SetCrossInstanceConfiguration> _respondToTimeZoneCommand;
    private Action<SetSystemConfiguration> _respondToSystemConfigurationCommand;

    public SummaryWizardPageSaveHandlerTests()
    {
        _targetConfigurationProvider
            .GetTargetConfiguration(Arg.Any<CancellationToken>())
            .Returns(_targetConfiguration);

        _initialPasswordProvider.GetAdministratorInitialPassword().Returns(InitialPassword);

        _userService
            .GetUsers(new UserName(AdministratorName), Arg.Any<CancellationToken>())
            .Returns([_administrator]);

        _userService
            .UpdateUser(Arg.Any<UserProfile>(), Arg.Any<CancellationToken>())
            .Returns(new UserManagementServiceSuccessResult());

        var instanceInformation = Substitute.For<IInstanceInformation>();
        instanceInformation.Id.Returns(Guid.NewGuid());
        _instanceInformationProvider.Local.Returns(instanceInformation);

        var onboardingState = new OnboardingState { InstanceId = instanceInformation.Id };
        _onboardingStateStore
            .GetOnboardingStateAsync(instanceInformation.Id, Arg.Any<CancellationToken>())
            .Returns(onboardingState);
        _onboardingStateStore
            .When(x => x.SetOnboardingStateAsync(Arg.Any<IOnboardingState>(), Arg.Any<CancellationToken>()))
            .Do(x => _completedWrites.Add(x.Arg<IOnboardingState>().Completed));

        _handler = new SummaryWizardPageSaveHandler(_targetConfigurationProvider, _userService, _initialPasswordProvider, _mediator,
            _onboardingStateStore, _instanceInformationProvider, NullLogger<SummaryWizardPageSaveHandler>.Instance);

        _respondToTimeZoneCommand = command => _handler.Consume(
            ClientContextFactory.Create(new CrossInstanceConfigurationChanged(command.CorrelationId, new CrossInstanceConfiguration())),
            CancellationToken.None);

        _respondToSystemConfigurationCommand = command => _handler.Consume(
            ClientContextFactory.Create(new SystemConfigurationChanged { CorrelationId = command.CorrelationId }),
            CancellationToken.None);

        // The response event is consumed inside Send, so the pending command is completed before the handler starts waiting.
        _mediator
            .When(x => x.Send(Arg.Any<SetCrossInstanceConfiguration>(), Arg.Any<CancellationToken>()))
            .Do(x => _respondToTimeZoneCommand(x.Arg<SetCrossInstanceConfiguration>()));

        _mediator
            .When(x => x.Send(Arg.Any<SetSystemConfiguration>(), Arg.Any<CancellationToken>()))
            .Do(x => _respondToSystemConfigurationCommand(x.Arg<SetSystemConfiguration>()));
    }

    public void Dispose()
        => _handler.Dispose();

    [Fact]
    public async Task Should_change_password_set_time_zone_and_complete_onboarding()
    {
        // Arrange
        var state = new SummaryWizardPageState();

        // Act
        var result = await _handler.Save(state, TestContext.Current.CancellationToken);

        // Assert
        result.Should().BeOfType<SaveSuccessResult>();

        await _userService.Received(1).UpdateUser(
            Arg.Is<UserProfile>(u => u.CurrentPassword == InitialPassword && u.NewPassword == NewPassword),
            Arg.Any<CancellationToken>());
        await _mediator.Received(1).Send(
            Arg.Is<SetCrossInstanceConfiguration>(c => c.TimeZoneId == s_timeZone.Id && c.CultureName == null),
            Arg.Any<CancellationToken>());
        await _mediator.DidNotReceive().Send(Arg.Any<SetSystemConfiguration>(), Arg.Any<CancellationToken>());

        _completedWrites.Should().Equal(true);
    }

    [Fact]
    public async Task Should_skip_password_change_without_credentials()
    {
        // Arrange
        _targetConfiguration.UserCredentials = null;
        var state = new SummaryWizardPageState();

        // Act
        var result = await _handler.Save(state, TestContext.Current.CancellationToken);

        // Assert
        result.Should().BeOfType<SaveSuccessResult>();
        await _userService.DidNotReceive().UpdateUser(Arg.Any<UserProfile>(), Arg.Any<CancellationToken>());
        _completedWrites.Should().Equal(true);
    }

    [Fact]
    public async Task Should_apply_new_system_configuration_after_completing_onboarding()
    {
        // Arrange
        var newSystemConfiguration = SystemConfigurations.CreateDhcp("new-hostname");
        var state = new SummaryWizardPageState { NewSystemConfiguration = newSystemConfiguration };

        // Act
        var result = await _handler.Save(state, TestContext.Current.CancellationToken);

        // Assert
        result.Should().BeOfType<SaveSuccessResult>();
        await _mediator.Received(1).Send(
            Arg.Is<SetSystemConfiguration>(c => c.SystemConfiguration == newSystemConfiguration),
            Arg.Any<CancellationToken>());
        _completedWrites.Should().Equal(true);
        state.CurrentOperation.Should().BeNull();
    }

    [Fact]
    public async Task Should_stop_before_time_zone_when_user_is_not_found()
    {
        // Arrange
        _userService
            .GetUsers(Arg.Any<UserName?>(), Arg.Any<CancellationToken>())
            .Returns([]);

        var state = new SummaryWizardPageState();

        // Act
        var result = await _handler.Save(state, TestContext.Current.CancellationToken);

        // Assert
        result.Should().BeOfType<SaveErrorResult>()
            .Which.Message.Should().Contain(AdministratorName);
        await _mediator.DidNotReceive().Send(Arg.Any<SetCrossInstanceConfiguration>(), Arg.Any<CancellationToken>());
        _completedWrites.Should().BeEmpty();
    }

    [Fact]
    public async Task Should_stop_before_time_zone_when_password_update_fails()
    {
        // Arrange
        _userService
            .UpdateUser(Arg.Any<UserProfile>(), Arg.Any<CancellationToken>())
            .Returns(new UserManagementServiceErrorResult("password rejected"));

        var state = new SummaryWizardPageState();

        // Act
        var result = await _handler.Save(state, TestContext.Current.CancellationToken);

        // Assert
        result.Should().BeOfType<SaveErrorResult>()
            .Which.Message.Should().Contain("password rejected");
        await _mediator.DidNotReceive().Send(Arg.Any<SetCrossInstanceConfiguration>(), Arg.Any<CancellationToken>());
        _completedWrites.Should().BeEmpty();
    }

    [Fact]
    public async Task Should_mark_onboarding_not_completed_when_time_zone_fails()
    {
        // Arrange
        _respondToTimeZoneCommand = command => _handler.Consume(
            ClientContextFactory.Create(new CrossInstanceConfigurationError(command.CorrelationId, new ErrorInfo(42, "time zone rejected"), null)),
            CancellationToken.None);

        var state = new SummaryWizardPageState { NewSystemConfiguration = SystemConfigurations.CreateDhcp() };

        // Act
        var result = await _handler.Save(state, TestContext.Current.CancellationToken);

        // Assert
        var error = result.Should().BeOfType<SaveErrorResult>().Subject;
        error.Message.Should().Be("time zone rejected");
        error.ErrorCode.Should().Be(42);

        await _mediator.DidNotReceive().Send(Arg.Any<SetSystemConfiguration>(), Arg.Any<CancellationToken>());
        _completedWrites.Should().Equal(false);
    }

    [Fact]
    public async Task Should_mark_onboarding_not_completed_when_system_configuration_fails()
    {
        // Arrange
        _respondToSystemConfigurationCommand = command => _handler.Consume(
            ClientContextFactory.Create(new SetSystemConfigurationError(command.CorrelationId, new ErrorInfo(7, "configuration rejected"))),
            CancellationToken.None);

        var state = new SummaryWizardPageState { NewSystemConfiguration = SystemConfigurations.CreateDhcp() };

        // Act
        var result = await _handler.Save(state, TestContext.Current.CancellationToken);

        // Assert
        result.Should().BeOfType<SaveErrorResult>()
            .Which.Message.Should().Be("configuration rejected");
        _completedWrites.Should().Equal(true, false);
        state.CurrentOperation.Should().BeNull();
    }

    [Fact]
    public async Task Should_ignore_events_of_other_commands()
    {
        // Arrange
        _respondToTimeZoneCommand = command =>
        {
            _handler.Consume(
                ClientContextFactory.Create(new CrossInstanceConfigurationError(Guid.NewGuid(), new ErrorInfo(1, "foreign error"), null)),
                CancellationToken.None);

            _handler.Consume(
                ClientContextFactory.Create(new CrossInstanceConfigurationChanged(command.CorrelationId, new CrossInstanceConfiguration())),
                CancellationToken.None);
        };

        var state = new SummaryWizardPageState();

        // Act
        var result = await _handler.Save(state, TestContext.Current.CancellationToken);

        // Assert
        result.Should().BeOfType<SaveSuccessResult>();
        _completedWrites.Should().Equal(true);
    }
}
