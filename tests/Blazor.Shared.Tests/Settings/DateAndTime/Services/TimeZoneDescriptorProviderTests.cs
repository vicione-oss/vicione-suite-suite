using System.Globalization;
using Blazor.Shared.Settings.DateAndTime.Services;

namespace Blazor.Shared.Tests.Settings.DateAndTime.Services;

public sealed class TimeZoneDescriptorProviderTests
{
    [Theory]
    [InlineData("UTC", "timezone_0.png")]
    [InlineData("Europe/Berlin", "timezone_1.png")]
    [InlineData("America/New_York", "timezone_-5.png")]
    [InlineData("Asia/Kabul", "timezone_4.5.png")]
    [InlineData("Asia/Kathmandu", "timezone_5.75.png")]
    [InlineData("America/St_Johns", "timezone_-3.5.png")]
    [InlineData("Pacific/Marquesas", "timezone_-9.5.png")]
    public async Task GetTimeZoneDescriptor_should_reference_image_of_base_utc_offset(string timeZoneId, string expectedFilename)
    {
        // Arrange
        CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("en-US");

        var provider = new TimeZoneDescriptorProvider();

        // Act
        var descriptor = await provider.GetTimeZoneDescriptor(timeZoneId, TestContext.Current.CancellationToken);

        // Assert
        descriptor.Should().NotBeNull();
        descriptor.Value.ImageUrl.OriginalString.Should().EndWith($"time-zones/{expectedFilename}");
    }
}
