using Blazor.Shared.Instance.ControlPanels.Update.Services;
using Blazor.Shared.Validation.Services.Validators;
using Core.Shared.HostManagement;
using Core.Shared.HostManagement.Commands;
using Core.Shared.HostManagement.Events;
using Microsoft.Extensions.Logging;
using Sdk.Client.ControlPanels.Models;
using Sdk.Client.Infrastructure;
using Sdk.Instance;
using Sdk.Messaging;
using CapabilityTexts = Blazor.Shared.Localization.HostManagementCapabilities;
using SaveHandlerTexts = Blazor.Shared.Instance.ControlPanels.Update.Localization.UpdateControlPanelSaveHandler;

namespace Blazor.Shared.Tests.Instance.ControlPanels.Update.Services;

public sealed class UpdateControlPanelSaveHandlerTests : IDisposable
{
    private const string Version = "2.0.0";

    private readonly IUiMediator _mediator = Substitute.For<IUiMediator>();
    private readonly IRequiredValidator _requiredValidator = Substitute.For<IRequiredValidator>();
    private readonly UpdateControlPanelSaveHandler _saveHandler;

    public UpdateControlPanelSaveHandlerTests()
    {
        _saveHandler = new UpdateControlPanelSaveHandler(_mediator,
            Substitute.For<IInstanceInformationProvider>(),
            _requiredValidator,
            Substitute.For<ILogger<UpdateControlPanelSaveHandler>>());
    }

    public void Dispose() => _saveHandler.Dispose();

    [Fact]
    public async Task Should_fail_with_the_install_text_when_installing_the_version_fails()
    {
        // Arrange
        _mediator.When(m => m.Send(Arg.Any<InstallSuiteVersion>(), Arg.Any<CancellationToken>()))
            .Do(async callInfo =>
            {
                var message = new InstallSuiteVersionStarted("Topic disabled", false)
                {
                    CorrelationId = callInfo.Arg<InstallSuiteVersion>().CorrelationId,
                    ErrorInfo = new ErrorInfo(2, "Topic disabled")
                };
                await _saveHandler.Consume(new ClientContext<InstallSuiteVersionStarted>(message, Guid.NewGuid()), CancellationToken.None);
            });

        var state = new UpdateControlPanelState
        {
            SuiteVersions = [new SuiteVersionPackage
            {
                Architecture = "arm64",
                HostManagementVersion = "2.0.0",
                PackageName = "suite.deb",
                SignatureName = "suite.deb.sig",
                Version = Version,
                Installed = false
            }],
            SelectedVersion = Version,
            SelectedVersionChanged = true
        };

        // Act
        var result = await _saveHandler.Save(state, TestContext.Current.CancellationToken);

        // Assert
        result.Should().BeOfType<SaveErrorResult>()
            .Which.Message.Should().StartWith(SaveHandlerTexts.InstallOfSelectedVersionFailed);
        state.SelectedVersionChanged.Should().BeTrue();
    }

    [Fact]
    public async Task Should_fail_with_the_update_text_when_updating_the_system_fails()
    {
        // Arrange
        _requiredValidator.Validate(Arg.Any<string?>(), Arg.Any<string>(), out Arg.Any<string?>()).Returns(true);
        _mediator.When(m => m.Send(Arg.Any<UpdateSystem>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>()))
            .Do(async callInfo =>
            {
                var message = new UpdateSystemStarted("Topic disabled", false)
                {
                    CorrelationId = callInfo.Arg<UpdateSystem>().CorrelationId,
                    ErrorInfo = new ErrorInfo(2, "Topic disabled")
                };
                await _saveHandler.Consume(new ClientContext<UpdateSystemStarted>(message, Guid.NewGuid()), CancellationToken.None);
            });

        var state = new UpdateControlPanelState
        {
            FlashDeviceEnabled = true,
            SwuFilenameUploaded = "image.swu"
        };

        // Act
        var result = await _saveHandler.Save(state, TestContext.Current.CancellationToken);

        // Assert
        result.Should().BeOfType<SaveErrorResult>()
            .Which.Message.Should().StartWith(SaveHandlerTexts.UpdateSystemFailed);
    }

    [Fact]
    public async Task Should_fail_with_the_function_disabled_text_when_host_management_disables_update_system()
    {
        // Arrange
        _requiredValidator.Validate(Arg.Any<string?>(), Arg.Any<string>(), out Arg.Any<string?>()).Returns(true);
        _mediator.When(m => m.Send(Arg.Any<UpdateSystem>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>()))
            .Do(async callInfo =>
            {
                var message = new UpdateSystemStarted(null, false)
                {
                    CorrelationId = callInfo.Arg<UpdateSystem>().CorrelationId,
                    ErrorInfo = new ErrorInfo(UpdateSystemStarted.UpdateSystemDisabled, "The capability 'UpdateSystem' is disabled in HostManagement.")
                };
                await _saveHandler.Consume(new ClientContext<UpdateSystemStarted>(message, Guid.NewGuid()), CancellationToken.None);
            });

        var state = new UpdateControlPanelState
        {
            FlashDeviceEnabled = true,
            SwuFilenameUploaded = "image.swu"
        };

        // Act
        var result = await _saveHandler.Save(state, TestContext.Current.CancellationToken);

        // Assert
        result.Should().BeOfType<SaveErrorResult>()
            .Which.Message.Should().Be(CapabilityTexts.FunctionDisabled);
    }
}
