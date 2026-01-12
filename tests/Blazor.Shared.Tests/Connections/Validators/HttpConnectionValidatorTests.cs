using Blazor.Shared.Connections.Validators;
using AwesomeAssertions;
using Sdk.Connections.Contracts;
using Xunit;

namespace Blazor.Shared.Tests.Connections.Validators;

public sealed class HttpConnectionValidatorTests
{
    [Theory]
    [InlineData("http://www.some.de")]
    [InlineData("https://www.some.de")]
    [InlineData("http://some.de")]
    [InlineData("https://some.de")]
    [InlineData("https://some.de/low/level")]
    [InlineData("https://some.de?reg=s&thing=1")]
    [InlineData("https://x.de.some.de?reg=s&thing=1")]
    public void Should_return_no_error_for_well_formed_url(string address)
    {
        // Act
        var connection = new HttpConnection { BaseAddress = address };
        var errors = new HttpConnectionValidator().Validate(connection);

        // Assert
        errors.Should().BeEmpty();
    }

    [Theory]
    [InlineData("a.b.c")]
    [InlineData("www.some.de")]
    [InlineData("://www.some.de")]
    [InlineData("_://www.some.de")]
    [InlineData("http:://some.de")]
    [InlineData("https:///some.de")]
    [InlineData("https://some..de?reg=s&thing=1")]
    [InlineData("https://some.de/low\\level")]
    [InlineData("https://x.de.some.de?reg=s &thing=1")]
    [InlineData("https://x.de.some.de&reg=s&thing=1")]
    public void Should_return_error_for_not_well_formed_url(string address)
    {
        // Act
        var connection = new HttpConnection { BaseAddress = address };
        var errors = new HttpConnectionValidator().Validate(connection);

        // Assert
        errors.Should().NotBeEmpty();
    }
}
