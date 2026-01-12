using Blazor.Shared.Connections.Contracts;
using Sdk.Client.ControlPanels.Services;

namespace Blazor.Shared.Connections.Services;

internal sealed class TagControlPanelResetHandler(ISuiteConnectionService connectionService) : IControlPanelResetHandler<TagControlPanelState>
{
    public async Task Reset(TagControlPanelState state, CancellationToken cancellationToken)
    {
        var tagId = state.TagId;
        if (tagId.HasValue)
        {
            state.BeginLoading();
            try
            {
                var tag = await connectionService.GetTag(tagId.Value, cancellationToken);
                if (tag is not null)
                    state.Tag = new EditTagModel(tag);
                else
                    state.Tag = null;
            }
            finally
            {
                state.EndLoading();
            }
        }
        else
        {
            state.Tag = new();
            state.IsNew = true;
        }
    }
}
