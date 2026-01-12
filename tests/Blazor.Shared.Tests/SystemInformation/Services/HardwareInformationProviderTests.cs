using System.Globalization;
using Blazor.Shared.SystemInformation.Services;
using AwesomeAssertions;
using Xunit;

namespace Blazor.Shared.Tests.SystemInformation.Services;

public class HardwareInformationProvider_FormatNumber
{
    [Theory]
    [InlineData(1, "1.0 KB")]
    [InlineData(1_024, "1.0 MB")]
    [InlineData(1_048_576, "1.0 GB")]
    [InlineData(1_073_741_824, "1.0 TB")]
    [InlineData(2_516_582, "2.4 GB")]
    public void Returns_with_one_number_after_decimal_point(double value, string expected)
        => HardwareInfoService.FormatNumber(value, CultureInfo.InvariantCulture).Should().Be(expected);
}

public class HardwareInformationProvider_RemoveAdditionalSigns
{
    [Theory]
    [InlineData("ARM Cortex-A72", "ARM Cortex-A72")]
    [InlineData("ARM Limited Cortex-A72", "ARM Cortex-A72")]
    [InlineData("Intel(R) Core(TM) Ultra 7 165H", "Intel Core Ultra 7 165H")]
    public void Returns_string_without_additional_signs(string value, string expected)
        => HardwareInfoService.RemoveAdditionalSigns(value).Should().Be(expected);
}
