using System.Net.NetworkInformation;
using Core.Shared.HostManagement.Extensions;

namespace Core.Shared.Tests.HostManagement.Extensions;

public sealed class PhysicalAddressExtensionsTests
{
    [Fact]
    public void Should_format_octets_as_padded_upper_case_hexadecimal_separated_by_colons()
    {
        // Arrange
        var address = new PhysicalAddress([0x00, 0x02, 0x0A, 0xB1, 0xCD, 0xEF]);

        // Act
        var notation = address.ToColonNotation();

        // Assert
        notation.Should().Be("00:02:0A:B1:CD:EF");
    }

    [Fact]
    public void Should_return_empty_string_when_address_has_no_octets()
    {
        // Act
        var notation = PhysicalAddress.None.ToColonNotation();

        // Assert
        notation.Should().BeEmpty();
    }

    [Fact]
    public void Should_produce_notation_that_parses_back_to_the_same_address()
    {
        // Arrange
        var address = new PhysicalAddress([0x00, 0x02, 0x01, 0x10, 0x53, 0x25]);

        // Act
        var parsed = PhysicalAddress.Parse(address.ToColonNotation());

        // Assert
        parsed.Should().Be(address);
    }
}
