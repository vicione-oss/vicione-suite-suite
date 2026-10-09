using Blazor.Shared.Onboarding.Models;
using Blazor.Shared.Onboarding.Services;
using Blazor.Shared.Settings.DateAndTime.Services;
using Blazor.Shared.UserInterface.ControlPanels.DateAndTime.Models;
using Sdk.Client.Wizards.Models;

namespace Blazor.Shared.Tests.Onboarding.Services;

public sealed class TimeZoneWizardPageSaveHandlerTests
{
    private const string BerlinTimeZoneId = "Europe/Berlin";

    private readonly ITimeZoneDescriptorProvider _timeZoneDescriptorProvider = Substitute.For<ITimeZoneDescriptorProvider>();
    private readonly ITargetConfigurationProvider _targetConfigurationProvider = Substitute.For<ITargetConfigurationProvider>();
    private readonly TargetConfiguration _targetConfiguration = new() { TimeZone = TimeZoneInfo.Utc };

    public TimeZoneWizardPageSaveHandlerTests()
        => _targetConfigurationProvider
            .GetTargetConfiguration(Arg.Any<CancellationToken>())
            .Returns(_targetConfiguration);

    private TimeZoneWizardPageSaveHandler CreateHandler() =>
        new(_timeZoneDescriptorProvider, _targetConfigurationProvider);

    private void SetupDescriptor(string selectedTimeZoneId, string descriptorTimeZoneId)
        => _timeZoneDescriptorProvider
            .GetTimeZoneDescriptor(selectedTimeZoneId, Arg.Any<CancellationToken>())
            .Returns(new TimeZoneDescriptor(descriptorTimeZoneId, descriptorTimeZoneId, TimeSpan.Zero, new Uri("https://localhost/flag.svg")));

    [Fact]
    public async Task Should_store_selected_time_zone_in_target_configuration()
    {
        // Arrange
        SetupDescriptor(BerlinTimeZoneId, BerlinTimeZoneId);

        var handler = CreateHandler();
        var state = new TimeZoneWizardPageState { SelectedTimeZoneId = BerlinTimeZoneId };

        // Act
        var result = await handler.Save(state, TestContext.Current.CancellationToken);

        // Assert
        result.Should().BeOfType<SaveSuccessResult>();
        _targetConfiguration.TimeZone.Should().Be(TimeZoneInfo.FindSystemTimeZoneById(BerlinTimeZoneId));
    }

    [Fact]
    public async Task Should_reject_time_zone_without_descriptor()
    {
        // Arrange
        _timeZoneDescriptorProvider
            .GetTimeZoneDescriptor(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((TimeZoneDescriptor?)null);

        var handler = CreateHandler();
        var state = new TimeZoneWizardPageState { SelectedTimeZoneId = "Unknown/Zone" };

        // Act
        var result = await handler.Save(state, TestContext.Current.CancellationToken);

        // Assert
        result.Should().BeOfType<SaveErrorResult>();
        _targetConfiguration.TimeZone.Should().Be(TimeZoneInfo.Utc);
    }

    [Fact]
    public async Task Should_reject_descriptor_without_system_time_zone()
    {
        // Arrange
        SetupDescriptor(BerlinTimeZoneId, "Unknown/Zone");

        var handler = CreateHandler();
        var state = new TimeZoneWizardPageState { SelectedTimeZoneId = BerlinTimeZoneId };

        // Act
        var result = await handler.Save(state, TestContext.Current.CancellationToken);

        // Assert
        result.Should().BeOfType<SaveErrorResult>();
        _targetConfiguration.TimeZone.Should().Be(TimeZoneInfo.Utc);
    }
}
