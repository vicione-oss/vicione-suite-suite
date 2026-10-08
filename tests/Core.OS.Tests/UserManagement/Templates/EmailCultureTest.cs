using Core.OS.UserManagement.Templates;

namespace Core.OS.Tests.UserManagement.Templates;

public class EmailCultureTest
{
    [Theory]
    [InlineData("de-DE", "en-US", "de-DE")]
    [InlineData("DE-de", "en-US", "de-DE")]
    [InlineData(null, "de-DE", "de-DE")]
    [InlineData("", "de-DE", "de-DE")]
    [InlineData("fr-FR", "de-DE", "de-DE")]
    [InlineData("not a culture", "de-DE", "de-DE")]
    [InlineData(null, "fr-FR", "en-US")]
    [InlineData(null, "not a culture", "en-US")]
    [InlineData(null, null, "en-US")]
    public void Should_resolve_user_language_then_instance_default_then_english(string? userLanguage,
        string? instanceCulture, string expectedCulture)
    {
        // Act
        var culture = EmailCulture.Resolve(userLanguage, instanceCulture);

        // Assert
        culture.Name.Should().Be(expectedCulture);
    }
}
