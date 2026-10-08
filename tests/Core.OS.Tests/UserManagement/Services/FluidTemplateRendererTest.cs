using System.IO.Abstractions;
using System.IO.Abstractions.TestingHelpers;
using System.Reflection;
using Core.OS.Modules.Services;
using Core.OS.UserManagement.Templates;
using Fluid;

namespace Core.OS.Tests.UserManagement.Services;

public class FluidTemplateRendererTest
{
    private static readonly MockFileSystem TestFileSystem = new();

    [Fact]
    public async Task Should_throw_on_missing_file()
    {
        // Arrange
        var renderer = new FluidTemplateRenderer(TestFileSystem);

        // Act + Assert
        await Assert.ThrowsAsync<FileNotFoundException>(() =>
            renderer.RenderFromTemplateFile(TestFileSystem.FileInfo.New("non-existent-template"),
                _ => { }, TestContext.Current.CancellationToken));
    }

    [Theory]
    [MemberData(nameof(GetUserManagementTemplates))]
    public async Task Should_render_template_when_template_exists(string templateFile)
    {
        // Arrange
        var renderer = new FluidTemplateRenderer(new FileSystem());

        // Act
        var result = await renderer.RenderFromTemplateFile(
            TestFileSystem.FileInfo.New(templateFile), _ => { }, TestContext.Current.CancellationToken);

        // Assert
        result.Should().NotBeEmpty();
    }

    [Fact]
    public async Task Should_html_encode_values()
    {
        // Arrange
        var fileSystem = new MockFileSystem();
        fileSystem.AddFile("template.liquid.html", new MockFileData("<p>{{ Name }}</p>"));
        var renderer = new FluidTemplateRenderer(fileSystem);

        // Act
        var result = await renderer.RenderFromTemplateFile(fileSystem.FileInfo.New("template.liquid.html"),
            context => context.SetValue("Name", "<a href='https://evil.example'>x</a>"),
            TestContext.Current.CancellationToken);

        // Assert
        result.Should().Be("<p>&lt;a href=&#x27;https://evil.example&#x27;&gt;x&lt;/a&gt;</p>");
    }

    public static TheoryData<string> GetUserManagementTemplates()
    {
        var templates = new UserManagementTemplates(new FileSystem());
        var data = new TheoryData<string>();
        var properties = typeof(UserManagementTemplates)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.PropertyType == typeof(IFileInfo));

        foreach (var property in properties)
        {
            data.Add(((IFileInfo)property.GetValue(templates)!).FullName);
        }

        return data;
    }
}
