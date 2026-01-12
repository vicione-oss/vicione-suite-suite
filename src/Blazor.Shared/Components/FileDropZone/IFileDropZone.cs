using Microsoft.AspNetCore.Components;

namespace Blazor.Shared.Components.FileDropZone;

internal interface IFileDropZone
{
    void SetInputFileElementReference(ElementReference? inputFileElementReference);
}
