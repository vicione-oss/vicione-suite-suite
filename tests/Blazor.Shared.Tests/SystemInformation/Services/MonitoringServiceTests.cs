using Blazor.Shared.SystemInformation.Services;
using AwesomeAssertions;
using Xunit;

namespace Blazor.Shared.Tests.SystemInformation.Services;

public class MonitoringService_UpdateValuesBuffer
{
    [Fact]
    public void Returns_false_when_temp_buffer_count_is_not_five()
    {
        var tempBuffer = new List<float> { 1f, 2f, 3f };
        var values = new List<float> { 10f, 20f };

        var result = MonitoringService.UpdateValuesBuffer(tempBuffer, values);

        result.Should().BeFalse();
        tempBuffer.Should().HaveCount(3);
        values.Should().Equal(10f, 20f);
    }

    [Fact]
    public void Returns_true_and_clears_temp_buffer_when_count_is_five()
    {
        var tempBuffer = new List<float> { 1f, 2f, 3f, 4f, 5f };
        var values = new List<float>();

        var result = MonitoringService.UpdateValuesBuffer(tempBuffer, values);

        result.Should().BeTrue();
        tempBuffer.Should().BeEmpty();
    }

    [Fact]
    public void Inserts_min_average_max_in_correct_order_when_count_is_five()
    {
        var tempBuffer = new List<float> { 1f, 3f, 5f, 7f, 9f };
        var values = new List<float>();

        var result = MonitoringService.UpdateValuesBuffer(tempBuffer, values);

        result.Should().BeTrue();
        values.Should().Equal(1f, 5f, 9f);
    }

    [Fact]
    public void Removes_last_three_when_values_count_equals_ValuesPerDay()
    {
        var tempBuffer = new List<float> { 2f, 4f, 6f, 8f, 10f };
        var values = Enumerable.Range(0, MonitoringService.ValuesPerDay).Select(i => (float)i).ToList();
        var originalTail = values.Skip(MonitoringService.ValuesPerDay - 3).Take(3).ToList();

        var result = MonitoringService.UpdateValuesBuffer(tempBuffer, values);

        result.Should().BeTrue();
        originalTail.ForEach(item => values.Should().NotContain(item));
        values.Should().HaveCount(MonitoringService.ValuesPerDay);
    }

    [Fact]
    public void Maintains_values_count_when_values_less_than_ValuesPerDay()
    {
        var tempBuffer = new List<float> { 1f, 1f, 1f, 1f, 1f };
        var values = new List<float> { 100f, 200f };

        var result = MonitoringService.UpdateValuesBuffer(tempBuffer, values);

        result.Should().BeTrue();
        values.Should().HaveCount(2 + 3);
    }
}
