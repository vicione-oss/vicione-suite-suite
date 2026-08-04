using Core.Shared.Passkeys;

namespace Core.Shared.Tests.Passkeys;

public class PasskeyIdConverterTest
{
    [Theory]
    [InlineData("null")]
    [InlineData("")]
    [InlineData("F4FE52BD-D156-4A2D-878C-31BB89692EEF")]
    public void Should_convert_back_and_forth(string someString)
    {
        // Arrange
        // Act
        var decoded = PasskeyIdConverter.DecodePasskeyId(someString);
        var encoded = PasskeyIdConverter.EncodePasskeyId(decoded);
        
        // Assert
        Assert.Equal(someString, encoded);
    }
}
