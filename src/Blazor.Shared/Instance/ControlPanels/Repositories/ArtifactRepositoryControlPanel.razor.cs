using Blazor.Shared.Instance.ControlPanels.Repositories.Extensions;
using Blazor.Shared.Instance.ControlPanels.Repositories.Services;
using Blazor.Shared.Services;
using Core.Shared.Instance.Contracts;
using Microsoft.AspNetCore.Components;
using Sdk.Authorization;
using Sdk.Client.ControlPanels.Attributes;
using Sdk.Client.ControlPanels.Components;
using Sdk.Messaging;
using ViciOne.Ui.MonochromeIcons.Core.Enums;
using ViciOne.Ui.MonochromeIcons.Core.Extensions;

namespace Blazor.Shared.Instance.ControlPanels.Repositories;

[ControlPanelCategory<ControlPanelSystemCategoryDescriptor>]
[ModuleAuthorize(SharedClientModule.ModuleId, AccessLevel.Full)]
public sealed partial class ArtifactRepositoryControlPanel : ControlPanelBase<ArtifactRepositoryControlPanelState>
{
    private readonly string _refreshIconCssClasses = MonochromeIconName.Refresh.GetCssClasses(MonochromeIconSize.Small).ToSpaceSeparated();

    [Inject] private IArtifactRepositoryClientService RepositoryService { get; set; } = default!;

    protected override Task OnInitializedAsync()
    {
        RepositoryService.RepositoryChanged += RepositoryChanged;
        return base.OnInitializedAsync();
    }

    protected override ValueTask DisposeAsyncCore()
    {
        RepositoryService.RepositoryChanged -= RepositoryChanged;
        return base.DisposeAsyncCore();
    }

    private async Task RepositoryChanged(ArtifactRepository source, CrudAction action)
    {
        if (action == CrudAction.Updated && source.Id == State.Repository?.Id)
        {
            State.UpdateRepository(source);
            await InvokeAsync(StateHasChanged);
        }
    }

    private async Task UpdateTokenButtonClick()
    {
        if (State.Repository is null)
            return;

        await RepositoryService.UpdateRepositoryToken(State.Repository);
    }

    private static bool IsDebugMode()
    {
#if DEBUG
        return true;
#else
        return false;
#endif
    }
}
