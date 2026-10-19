using Blazor.Shared.Settings.Extensions;

namespace Blazor.Shared.Tests.Settings.Extensions;

public class ICollectionExtensionsTests
{
    public sealed class ToListAndClear
    {
        [Fact]
        public void Should_return_the_items_and_clear_the_collection()
        {
            // Arrange
            List<int> collection = [1, 2, 3];

            // Act
            var result = collection.ToListAndClear();

            // Assert
            result.Should().Equal(1, 2, 3);
            collection.Should().BeEmpty();
        }

        [Fact]
        public void Should_return_an_empty_list_for_an_empty_collection()
        {
            // Arrange
            List<int> collection = [];

            // Act
            var result = collection.ToListAndClear();

            // Assert
            result.Should().BeEmpty();
        }
    }
}
