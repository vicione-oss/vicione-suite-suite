using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

namespace Blazor.Shared.Help.NotificationArea;

public sealed partial class HelpDetail
{
    private string[] _helpText = [];

    [Parameter]
    public Contracts.HelpModel? Help { get; set; }

    [Parameter]
    public EventCallback<Guid> OnLinkClick { get; set; }

    private RenderFragment GetFragment(IComponent owner, string link) => builder =>
    {
        var attributes = link.Remove(0, 5);
        var splittedAttributes = attributes.Split(' ');
        var text = splittedAttributes[0].Split("text:").Last();
        var id = splittedAttributes[1].Split("helpId:").Last();

        builder.OpenElement(1, "a");
        builder.AddAttribute(2, "href", "#");
        builder.AddAttribute(3, "onclick",
            EventCallback.Factory.Create(owner,
               async () => await OnLinkClick.InvokeAsync(Guid.Parse(id))));
        builder.AddEventPreventDefaultAttribute(4, "onclick", true);
        builder.AddContent(5, text);
        builder.CloseElement();
    };

    protected override void OnParametersSet()
        => _helpText = Help!.Text.Split('<', '>');
}
