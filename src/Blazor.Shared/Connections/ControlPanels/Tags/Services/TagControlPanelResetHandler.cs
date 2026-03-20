using Blazor.Shared.Connections.Contracts;
using Blazor.Shared.Connections.Services;
using Sdk.Client.ControlPanels.Services;

namespace Blazor.Shared.Connections.ControlPanels.Tags.Services;

internal sealed class TagControlPanelResetHandler(ISuiteConnectionService connectionService) : IControlPanelResetHandler<TagControlPanelState>
{
    public async Task Reset(TagControlPanelState state, CancellationToken cancellationToken)
    {
        state.BeginLoading();
        try
        {
            var tagId = state.TagId;
            if (tagId.HasValue)
            {
                var tag = await connectionService.GetTag(tagId.Value, cancellationToken);
                if (tag is not null)
                    state.Tag = new EditTagModel(tag);
                else
                    state.Tag = null;
            }
            else
            {
                state.Tag = new();
                state.IsNew = true;
            }
        }
        finally
        {
            state.EndLoading();
        }
    }
}
