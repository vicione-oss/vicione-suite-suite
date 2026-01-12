using System.IO.Abstractions;
using Fluid;

namespace Core.OS.Modules.Services;

public sealed class FluidTemplateRenderer
{
    private readonly FluidParser fluidParser = new();

    public async Task<string> RenderFromTemplateFile(IFileInfo templateFile,
        Action<TemplateContext> contextAction,
        CancellationToken cancellationToken = default)
    {
        var fileContents = await File.ReadAllTextAsync(templateFile.FullName, cancellationToken);
        return await RenderTemplate(fileContents, contextAction);
    }

    private async Task<string> RenderTemplate(string templateFileContents, Action<TemplateContext> contextAction)
    {
        var template = fluidParser.Parse(
            templateFileContents);

        var templateContext = new TemplateContext();
        contextAction(templateContext);

        return await template.RenderAsync(templateContext);
    }
}
