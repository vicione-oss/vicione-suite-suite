using Blazor.Shared.Onboarding.Models;
using Blazor.Shared.Onboarding.Services;
using Core.Shared.HostManagement;
using HostManagement.Shared.Contracts;
using HostManagement.Shared.Contracts.Network;
using Sdk.Client.Infrastructure;
using Sdk.Instance;

namespace Blazor.Shared.Tests.Onboarding.Services;

public sealed class SummaryWizardPageResetHandlerTests
{
    private readonly ITargetConfigurationProvider _targetConfigurationProvider = Substitute.For<ITargetConfigurationProvider>();
    private readonly IInstanceInformationProvider _instanceInformationProvider = Substitute.For<IInstanceInformationProvider>();
    private readonly IUiMediator _mediator = Substitute.For<IUiMediator>();

    public SummaryWizardPageResetHandlerTests()
    {
        _targetConfigurationProvider
            .GetTargetConfiguration(Arg.Any<CancellationToken>())
            .Returns(new TargetConfiguration());

        _instanceInformationProvider.Local
            .Returns(Substitute.For<IInstanceInformation>());

        _mediator
            .Request<GetHostMgmtSystemConfiguration, GetHostMgmtSystemConfigurationResponse>(
                Arg.Any<GetHostMgmtSystemConfiguration>(), Arg.Any<CancellationToken>())
            .Returns(new GetHostMgmtSystemConfigurationResponse
            {
                Configuration = new SystemConfiguration
                {
                    NetworkInterfaces =
                    [
                        new NetworkInterfaceDetail
                        {
                            CommonInformation = new NetworkInterfaceCommonInformation { Name = "lan1", Enabled = true },
                            IPv4 = new IPv4Settings { DHCPEnabled = true }
                        },
                        new NetworkInterfaceDetail
                        {
                            CommonInformation = new NetworkInterfaceCommonInformation { Name = "lan2", Enabled = true },
                            IPv4 = new IPv4Settings { DHCPEnabled = true }
                        }
                    ]
                }
            });
    }

    private SummaryWizardPageResetHandler CreateHandler() =>
        new(_targetConfigurationProvider, _instanceInformationProvider, _mediator);

    [Fact]
    public async Task Should_complete_without_error()
    {
        // Arrange
        var handler = CreateHandler();
        var state = new SummaryWizardPageState();

        // Act
        await handler.Reset(state, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Should_request_system_configuration_from_mediator()
    {
        // Arrange
        var handler = CreateHandler();
        var state = new SummaryWizardPageState();

        // Act
        await handler.Reset(state, TestContext.Current.CancellationToken);

        // Assert
        await _mediator.Received(1).Request<GetHostMgmtSystemConfiguration, GetHostMgmtSystemConfigurationResponse>(
            Arg.Any<GetHostMgmtSystemConfiguration>(), Arg.Any<CancellationToken>());
    }
}
