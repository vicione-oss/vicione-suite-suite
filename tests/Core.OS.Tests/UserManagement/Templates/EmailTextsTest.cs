using System.Collections;
using System.Globalization;
using System.Text.RegularExpressions;
using Core.OS.Tests.UserManagement.Services;
using Core.OS.UserManagement.Templates;
using Core.OS.UserManagement.Templates.Localization;

namespace Core.OS.Tests.UserManagement.Templates;

public partial class EmailTextsTest
{
    [Fact]
    public void Should_translate_every_text_to_german()
    {
        // Arrange
        var englishNames = GetNames(CultureInfo.InvariantCulture);

        // Act
        var germanNames = GetNames(CultureInfo.GetCultureInfo("de"));

        // Assert
        germanNames.Should().BeEquivalentTo(englishNames);
    }

    [Theory]
    [MemberData(nameof(FluidTemplateRendererTest.GetUserManagementTemplates), MemberType = typeof(FluidTemplateRendererTest))]
    public void Should_define_every_text_used_by_template(string templateFile)
    {
        // Arrange
        var names = EmailTextLookup.GetAll(CultureInfo.InvariantCulture).Keys;

        // Act
        var usedNames = TextReference().Matches(File.ReadAllText(templateFile)).Select(match => match.Groups[1].Value);

        // Assert
        usedNames.Should().NotBeEmpty().And.BeSubsetOf(names);
    }

    private static string[] GetNames(CultureInfo culture)
        => EmailTexts.ResourceManager.GetResourceSet(culture, createIfNotExists: true, tryParents: false)!
            .Cast<DictionaryEntry>()
            .Select(entry => (string)entry.Key)
            .ToArray();

    [GeneratedRegex(@"\{\{\s*Text\.(\w+)")]
    private static partial Regex TextReference();
}
