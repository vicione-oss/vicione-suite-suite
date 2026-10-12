using System.Globalization;
using Blazor.Shared.Extensions;

namespace Blazor.Shared.Tests.Extensions;

public sealed class IntExtensionsTests
{
    [Theory]
    [InlineData("en-US")]
    [InlineData("de-DE")]
    [InlineData("sv-SE")]
    [InlineData("fi-FI")]
    [InlineData("nb-NO")]
    public void ToAttributeValue_should_format_negative_number_with_ascii_minus_in_any_culture(string cultureName)
    {
        // Arrange
        var originalCulture = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = new CultureInfo(cultureName);

        try
        {
            // Act
            var result = (-100).ToAttributeValue("%");

            // Assert
            result.Should().Be("-100%");
        }
        finally
        {
            CultureInfo.CurrentCulture = originalCulture;
        }
    }

    [Fact]
    public void ToAttributeValue_should_format_number_without_addition()
    {
        // Arrange
        const int number = 1234567;

        // Act
        var result = number.ToAttributeValue();

        // Assert
        result.Should().Be("1234567");
    }
}
