using Blazor.Shared.SystemInformation.Services;
using AwesomeAssertions;
using Xunit;

namespace Blazor.Shared.Tests.SystemInformation.Services;

public sealed class MonitoringService_UpdateValuesBuffer
{
    [Fact]
    public void Should_return_false_when_temp_buffer_count_is_not_five()
    {
        // Arrange
        var tempBuffer = new List<float> { 1f, 2f, 3f };
        var values = new List<float> { 10f, 20f };

        // Act
        var result = MonitoringService.UpdateValuesBuffer(tempBuffer, values);

        // Assert
        result.Should().BeFalse();
        tempBuffer.Should().HaveCount(3);
        values.Should().Equal(10f, 20f);
    }

    [Fact]
    public void Should_return_true_and_clear_temp_buffer_when_count_is_five()
    {
        // Arrange
        var tempBuffer = new List<float> { 1f, 2f, 3f, 4f, 5f };
        var values = new List<float>();

        // Act
        var result = MonitoringService.UpdateValuesBuffer(tempBuffer, values);

        // Assert
        result.Should().BeTrue();
        tempBuffer.Should().BeEmpty();
    }

    [Fact]
    public void Should_insert_min_average_max_in_correct_order_when_count_is_five()
    {
        // Arrange
        var tempBuffer = new List<float> { 1f, 3f, 5f, 7f, 9f };
        var values = new List<float>();

        // Act
        var result = MonitoringService.UpdateValuesBuffer(tempBuffer, values);

        // Assert
        result.Should().BeTrue();
        values.Should().Equal(1f, 5f, 9f);
    }

    [Fact]
    public void Should_remove_last_three_when_values_count_equals_values_per_day()
    {
        // Arrange
        var tempBuffer = new List<float> { 2f, 4f, 6f, 8f, 10f };
        var values = Enumerable.Range(0, MonitoringService.ValuesPerDay).Select(i => (float)i).ToList();
        var originalTail = values.Skip(MonitoringService.ValuesPerDay - 3).Take(3).ToList();

        // Act
        var result = MonitoringService.UpdateValuesBuffer(tempBuffer, values);

        // Assert
        result.Should().BeTrue();
        originalTail.ForEach(item => values.Should().NotContain(item));
        values.Should().HaveCount(MonitoringService.ValuesPerDay);
    }

    [Fact]
    public void Should_maintain_values_count_when_values_less_than_values_per_day()
    {
        // Arrange
        var tempBuffer = new List<float> { 1f, 1f, 1f, 1f, 1f };
        var values = new List<float> { 100f, 200f };

        // Act
        var result = MonitoringService.UpdateValuesBuffer(tempBuffer, values);

        // Assert
        result.Should().BeTrue();
        values.Should().HaveCount(2 + 3);
    }
}
