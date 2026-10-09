using Blazor.Shared.Onboarding.Models;
using Blazor.Shared.Onboarding.Services;
using Blazor.Shared.Onboarding.Services.Validators;
using Sdk.Client.Wizards.Models;

namespace Blazor.Shared.Tests.Onboarding.Services;

public sealed class HostnameWizardPageSaveHandlerTests
{
    private readonly IHostnameValidator _hostnameValidator = Substitute.For<IHostnameValidator>();
    private readonly ITargetConfigurationProvider _targetConfigurationProvider = Substitute.For<ITargetConfigurationProvider>();
    private readonly TargetConfiguration _targetConfiguration = new() { Hostname = "initial" };

    public HostnameWizardPageSaveHandlerTests()
    {
        _hostnameValidator
            .Validate(Arg.Any<string>(), Arg.Any<string>(), out Arg.Any<string?>())
            .Returns(true);

        _targetConfigurationProvider
            .GetTargetConfiguration(Arg.Any<CancellationToken>())
            .Returns(_targetConfiguration);
    }

    private HostnameWizardPageSaveHandler CreateHandler() =>
        new(_hostnameValidator, _targetConfigurationProvider);

    [Fact]
    public async Task Should_store_hostname_in_target_configuration()
    {
        // Arrange
        var handler = CreateHandler();
        var state = new HostnameWizardPageState { Hostname = "edge-s-01" };

        // Act
        var result = await handler.Save(state, TestContext.Current.CancellationToken);

        // Assert
        result.Should().BeOfType<SaveSuccessResult>();
        _targetConfiguration.Hostname.Should().Be("edge-s-01");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Should_reject_missing_hostname(string? hostname)
    {
        // Arrange
        var handler = CreateHandler();
        var state = new HostnameWizardPageState { Hostname = hostname };

        // Act
        var result = await handler.Save(state, TestContext.Current.CancellationToken);

        // Assert
        result.Should().BeOfType<SaveErrorResult>();
        _targetConfiguration.Hostname.Should().Be("initial");
        _hostnameValidator.DidNotReceive().Validate(Arg.Any<string>(), Arg.Any<string>(), out Arg.Any<string?>());
    }

    [Fact]
    public async Task Should_accept_hostname_of_maximum_length()
    {
        // Arrange
        var handler = CreateHandler();
        var state = new HostnameWizardPageState();
        state.Hostname = new string('a', state.HostnameMaximumLength);

        // Act
        var result = await handler.Save(state, TestContext.Current.CancellationToken);

        // Assert
        result.Should().BeOfType<SaveSuccessResult>();
    }

    [Fact]
    public async Task Should_reject_hostname_longer_than_maximum_length()
    {
        // Arrange
        var handler = CreateHandler();
        var state = new HostnameWizardPageState();
        state.Hostname = new string('a', state.HostnameMaximumLength + 1);

        // Act
        var result = await handler.Save(state, TestContext.Current.CancellationToken);

        // Assert
        result.Should().BeOfType<SaveErrorResult>();
        _targetConfiguration.Hostname.Should().Be("initial");
    }

    [Fact]
    public async Task Should_return_validator_message_when_hostname_is_invalid()
    {
        // Arrange
        _hostnameValidator
            .Validate(Arg.Any<string>(), Arg.Any<string>(), out Arg.Any<string?>())
            .Returns(x =>
            {
                x[2] = "invalid hostname";
                return false;
            });

        var handler = CreateHandler();
        var state = new HostnameWizardPageState { Hostname = "-edge" };

        // Act
        var result = await handler.Save(state, TestContext.Current.CancellationToken);

        // Assert
        result.Should().BeOfType<SaveErrorResult>()
            .Which.Message.Should().Be("invalid hostname");
        _targetConfiguration.Hostname.Should().Be("initial");
    }
}
