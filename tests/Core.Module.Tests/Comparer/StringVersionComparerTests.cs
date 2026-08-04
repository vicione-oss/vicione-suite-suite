using Core.Module.Comparer;

namespace Core.Module.Tests.Comparer;

public class StringVersionComparerTests
{
    private readonly StringVersionComparer _comparer = new();

    private readonly List<string> versions =
    [
        "4.2.2",
        "1.1.51",
        "4.2.2-ci1111111",

        "4.2.3",
        "0.3.5",

        "4.2.2-ci2222222",
        "4.2.3-ci1111111",
    ];

    [Fact]
    public void Should_sort_correctly_descending()
    {
        // Arrange
        var expected = new List<string>
        {
            "4.2.3",
            "4.2.3-ci1111111",

            "4.2.2",
            "4.2.2-ci2222222",
            "4.2.2-ci1111111",

            "1.1.51",
            "0.3.5",
        };

        // Act
        versions.Sort((x, y) => _comparer.Compare(y, x));

        // Assert
        versions.Should().Equal(expected);
    }

    [Fact]
    public void Should_sort_correctly_ascending()
    {
        // Arrange
        var expected = new List<string>
        {
            "0.3.5",
            "1.1.51",

            "4.2.2-ci1111111",
            "4.2.2-ci2222222",
            "4.2.2",

            "4.2.3-ci1111111",
            "4.2.3",
        };

        // Act
        versions.Sort(_comparer);

        // Assert
        versions.Should().Equal(expected);
    }

    [Theory]
    [InlineData("4.2.2", "4.2.2")]
    [InlineData("0.2.2-ci2222222", "0.2.2-ci2222222")]
    public void Should_return_zero_if_versions_are_equal(string version1, string version2)
    {
        // Act + Assert
        _comparer.Compare(version1, version2).Should().Be(0);
    }

    [Theory]
    [InlineData("4.2.1", "4.2.2")]
    [InlineData("2.2.1", "4.2.2")]
    [InlineData("4.2.2-ci2222222", "4.2.2")]
    [InlineData("4.2.2-ci2222222", "4.3.2")]
    [InlineData("4.2.2-ci1111111", "4.2.2-ci2222222")]
    public void Should_return_minus_one_if_version1_is_lower_than_version2(string version1, string version2)
    {
        // Act + Assert
        _comparer.Compare(version1, version2).Should().Be(-1);
    }

    [Theory]
    [InlineData("4.2.2", "4.2.1")]
    [InlineData("4.2.2", "2.2.1")]
    [InlineData("4.3.2", "4.2.2-ci2222222")]
    [InlineData("4.2.2", "4.2.2-ci2222222")]
    [InlineData("4.2.2-ci2222222", "4.2.2-ci1111111")]
    [InlineData("4.2.2-ci1000002", "4.2.2-ci1000001")]
    public void Should_return_one_if_version1_is_greater_than_version2(string version1, string version2)
    {
        // Act + Assert
        _comparer.Compare(version1, version2).Should().Be(1);
    }
}
