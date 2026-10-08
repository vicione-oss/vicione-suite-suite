using Sdk.Connections.Contracts;

namespace Blazor.Shared.Connections.Contracts;

internal class EditTagModel
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Text { get; set; } = string.Empty;
    public bool Protected { get; set; }

    public EditTagModel()
    {
    }

    public EditTagModel(Tag tag)
    {
        Id = tag.Id;
        Text = tag.Text;
        Protected = tag.Protected;
    }
}
