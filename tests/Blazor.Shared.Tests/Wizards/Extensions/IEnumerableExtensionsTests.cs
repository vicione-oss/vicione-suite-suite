using AwesomeAssertions;
using Blazor.Shared.Wizards.Extensions;
using Xunit;
using Xunit.Sdk;

namespace Blazor.Shared.Tests.Wizards.Extensions;

public sealed class IEnumerableExtensionsTests
{
    public class AssertReturnValueContext : IXunitSerializable
    {
        public required int ItemCount { get; set; }
        public required int Start { get; set; }
        public required IEnumerable<int> Expected { get; set; }

        public void Serialize(IXunitSerializationInfo info)
        {
            info.AddValue(nameof(ItemCount), ItemCount);
            info.AddValue(nameof(Start), Start);
            info.AddValue(nameof(Expected), Expected.ToArray());
        }

        public void Deserialize(IXunitSerializationInfo info)
        {
            ItemCount = info.GetValue<int>(nameof(ItemCount));
            Start = info.GetValue<int>(nameof(Start));
            Expected = info.GetValue<int[]>(nameof(Expected)) ?? [];
        }
    }

    public static TheoryData<AssertReturnValueContext> AssertReturnValueContexts =>
    [
        new AssertReturnValueContext { ItemCount = 6, Start = 1, Expected = [.. Enumerable.Range(1, 6)] },
        new AssertReturnValueContext { ItemCount = 5, Start = 3, Expected = [.. Enumerable.Range(1, 5)] },
        new AssertReturnValueContext { ItemCount = 7, Start = 2, Expected = [1, 2, 3, 4, 5, 7] },
        new AssertReturnValueContext { ItemCount = 7, Start = 4, Expected = [1, 3, 4, 5, 6, 7] },
        new AssertReturnValueContext { ItemCount = 7, Start = 7, Expected = [1, 3, 4, 5, 6, 7] },
        new AssertReturnValueContext { ItemCount = 12, Start = 2, Expected = [1, 2, 3, 4, 5, 12] },
        new AssertReturnValueContext { ItemCount = 12, Start = 5, Expected = [1, 4, 5, 6, 7, 12] }
    ];

    [Theory]
    [MemberData(nameof(AssertReturnValueContexts))]
    public void Assert_returned_value(AssertReturnValueContext context)
    {
        // Arrange
        var steps = Enumerable.Range(1, context.ItemCount);

        // Act
        var result = steps.Reduce(context.Start, maximumItems: 6);

        // Assert
        result.Should().BeEquivalentTo(context.Expected);
    }

    [Fact]
    public void Should_throw_on_invalid_start()
    {
        // Arrange
        var steps = Enumerable.Range(1, 6);

        // Act
        var act = () => steps.Reduce(start: 13, maximumItems: 6);

        // Assert
        act.Should().ThrowExactly<ArgumentException>();
    }
}
