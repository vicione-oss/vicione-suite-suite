using System.IO.Abstractions;
using System.Text.Encodings.Web;
using System.Text.Unicode;
using Fluid;

namespace Core.OS.Modules.Services;

public sealed class FluidTemplateRenderer(IFileSystem fileSystem)
{
    /// <summary>Encodes markup characters of rendered values, but keeps umlauts readable in the mail source.</summary>
    private static readonly HtmlEncoder Encoder = HtmlEncoder.Create(UnicodeRanges.All);

    private readonly FluidParser _fluidParser = new();

    public async Task<string> RenderFromTemplateFile(IFileInfo templateFile,
        Action<TemplateContext> contextAction,
        CancellationToken cancellationToken = default)
    {
        var fileContents = await fileSystem.File.ReadAllTextAsync(templateFile.FullName, cancellationToken);
        return await RenderTemplate(fileContents, contextAction);
    }

    private async Task<string> RenderTemplate(string templateFileContents, Action<TemplateContext> contextAction)
    {
        var template = _fluidParser.Parse(
            templateFileContents);

        var templateContext = new TemplateContext();
        contextAction(templateContext);

        return await template.RenderAsync(templateContext, Encoder);
    }
}
