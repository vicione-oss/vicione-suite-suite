using Microsoft.AspNetCore.Components;
using Sdk.Client.Components.Cards.Contracts;
using Sdk.Client.Modules;
using Sdk.Client.NotificationArea.Components;

namespace Blazor.Shared.Help.NotificationArea;

public sealed partial class HelpNotificationElementFlyoutContent : ComponentBase, INotificationElementFlyoutContent
{
    private List<Contracts.HelpModel> _helpItems = [];
    private string _filter = string.Empty;
    private bool _listView = true;
    private Contracts.HelpModel? _help;
    private readonly Stack<Contracts.HelpModel?> _stack = new();

    protected override void OnInitialized()
    {
        var teaserImagePath = ModuleAssetHelper.GetModuleImagePath<SharedClientModule>("teaser-image.png");

        _helpItems.Add(new Contracts.HelpModel
        {
            Id = Guid.Parse("14c68d5a-68fa-47b2-bafd-f0ecb62223ea"),
            Title = "Help 1",
            TeaserText = "Lorem ipsum dolor sit amet, consetetur sadipscing elitr, sed diam nonumy eirmod tempor invidunt " +
                "ut labore et dolore magna aliquyam erat, sed diam voluptua. At vero eos et accusam et",
            Text = "Lorem ipsum dolor sit amet, consetetur <link text:sadipscing helpId:dc7341ae-1b0d-483c-8512-790fde054a80> elitr, sed diam nonumy eirmod tempor invidunt " +
                "ut labore et dolore magna aliquyam erat, sed diam voluptua. At vero eos et accusam et",
            TeaserImagePath = teaserImagePath
        });
        _helpItems.Add(new Contracts.HelpModel
        {
            Id = Guid.Parse("36808a03-2fd2-4a6e-a2d4-ea8ed68d13a1"),
            Title = "Help 2",
            TeaserText = "Lorem ipsum dolor sit amet, consetetur sadipscing elitr, sed diam nonumy eirmod tempor invidunt " +
                "ut labore et dolore magna aliquyam erat, sed diam voluptua. At vero eos et accusam et",
            Text = "Lorem ipsum dolor sit amet, consetetur <link text:sadipscing helpId:14c68d5a-68fa-47b2-bafd-f0ecb62223ea> elitr, sed diam nonumy eirmod tempor invidunt " +
                "ut labore et dolore magna aliquyam erat, sed diam voluptua. At vero eos et accusam et",
            TeaserImagePath = string.Empty
        });
        _helpItems.Add(new Contracts.HelpModel
        {
            Id = Guid.Parse("dc7341ae-1b0d-483c-8512-790fde054a80"),
            Title = "Help 3",
            TeaserText = "Lorem ipsum dolor sit amet, consetetur sadipscing elitr, sed diam nonumy eirmod tempor invidunt " +
                "ut labore et dolore magna aliquyam erat, sed diam voluptua. At vero eos et accusam et justo duo dolores " +
                "et ea rebum. Stet clita kasd gubergren",
            Text = "Lorem ipsum dolor sit amet, consetetur <link text:sadipscing helpId:14c68d5a-68fa-47b2-bafd-f0ecb62223ea> elitr, sed diam nonumy eirmod tempor invidunt " +
                "ut labore et dolore magna aliquyam erat, sed diam voluptua. At vero eos et accusam et justo duo dolores et ea rebum. Stet clita kasd gubergren, no sea takimata " +
                "sanctus est Lorem ipsum dolor sit amet.\r\n\r\nLorem ipsum dolor sit amet, consetetur sadipscing elitr, sed diam nonumy eirmod tempor invidunt ut labore et dolore " +
                "magna aliquyam erat, sed diam voluptua. At vero eos et accusam et justo duo dolores et ea rebum. Stet clita kasd gubergren, no sea " +
                "<link text:takimata helpId:36808a03-2fd2-4a6e-a2d4-ea8ed68d13a1> sanctus est Lorem ipsum dolor sit amet. Lorem ipsum dolor sit amet, consetetur sadipscing " +
                "elitr, sed diam nonumy eirmod tempor invidunt ut labore et dolore magna aliquyam erat, sed diam voluptua. At vero eos et accusam et justo duo dolores et ea rebum. " +
                "Stet clita kasd gubergren, no sea takimata sanctus est Lorem ipsum dolor sit amet.   \r\n\r\nDuis autem vel eum iriure dolor in hendrerit in vulputate velit esse " +
                "molestie consequat, vel illum dolore eu feugiat nulla facilisis at vero eros et accumsan et iusto odio dignissim qui blandit praesent luptatum zzril delenit augue " +
                "duis dolore te feugait nulla facilisi. Lorem ipsum dolor sit amet, consectetuer adipiscing elit, sed diam nonummy nibh euismod tincidunt ut laoreet dolore magna aliquam " +
                "erat volutpat.   \r\n\r\nUt wisi enim ad minim veniam, quis nostrud exerci tation ullamcorper suscipit lobortis nisl ut aliquip ex ea commodo consequat. Duis autem vel " +
                "eum iriure dolor in hendrerit in vulputate velit esse molestie consequat, vel illum dolore eu feugiat nulla facilisis at vero eros et accumsan et iusto odio dignissim qui " +
                "blandit praesent luptatum zzril delenit augue duis dolore te feugait nulla facilisi.   \r\n\r\nNam liber tempor cum soluta nobis eleifend option congue nihil imperdiet " +
                "doming id quod mazim placerat facer possim assum. Lorem ipsum dolor sit amet, consectetuer adipiscing elit, sed diam nonummy nibh euismod tincidunt ut laoreet dolore magna " +
                "aliquam erat volutpat. Ut wisi enim ad minim veniam, quis nostrud exerci tation <link text:ullamcorper helpId:14c68d5a-68fa-47b2-bafd-f0ecb62223ea> suscipit lobortis nisl ut " +
                "aliquip ex ea commodo consequat.   \r\n\r\nDuis autem vel eum iriure dolor in hendrerit in vulputate velit esse molestie consequat, vel illum dolore eu feugiat nulla " +
                "facilisis.   \r\n\r\nAt vero eos et accusam et justo duo dolores et ea rebum. Stet clita kasd gubergren, no sea takimata sanctus est Lorem ipsum dolor sit amet. " +
                "Lorem ipsum dolor sit amet, consetetur",
            TeaserImagePath = teaserImagePath
        });
        _helpItems.Add(new Contracts.HelpModel
        {
            Id = Guid.Parse("5a59c1fc-51a6-420e-83fe-cdc94bbc597d"),
            Title = "Help 4",
            TeaserText = "Lorem ipsum dolor sit amet, consetetur sadipscing elitr, sed diam nonumy eirmod tempor invidunt " +
                "ut labore et dolore magna aliquyam erat, sed diam voluptua. At vero eos et accusam et",
            Text = string.Empty,
            TeaserImagePath = string.Empty
        });
    }

    private async Task OnExecuteSearch()
        => await Reload();

    private async Task Reload()
        => await InvokeAsync(StateHasChanged);

    private IEnumerable<Contracts.HelpModel> GetHelpItems()
        => string.IsNullOrEmpty(_filter)
        ? _helpItems
        : _helpItems.Where(
            h => h.Title.Contains(_filter, StringComparison.Ordinal) ||
            h.TeaserText.Contains(_filter, StringComparison.Ordinal));

    private async Task OnArrowBackClick()
    {
        if (_stack.Count == 0)
        {
            await ShowCardView();
            return;
        }

        _help = _stack.Pop();

        await Reload();
    }

    private async Task ShowCardView()
    {
        _filter = string.Empty;
        _listView = true;
        _stack.Clear();

        await Reload();
    }

    private async Task OnLinkClick(Guid id)
    {
        _stack.Push(_help);
        _help = _helpItems.First(h => h.Id == id);

        await Reload();
    }

    private async Task OnHelpClick(ICardModel help)
    {
        _listView = false;
        _help = (Contracts.HelpModel)help;

        await Reload();
    }

    private async Task OnCloseClick(ICardModel help)
    {
        _helpItems = _helpItems.Where(h => h.Id != help.Id).ToList();

        await Reload();
    }
}
