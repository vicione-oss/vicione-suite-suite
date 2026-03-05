using Blazor.Shared.Connections.Services;
using Sdk.Client.ControlPanels.Models;
using Sdk.Client.ControlPanels.Services;
using Sdk.Connections.Contracts;

namespace Blazor.Shared.Connections.ControlPanels.Tags.Services;

internal sealed class TagControlPanelSaveHandler(ISuiteConnectionService connectionService)
    : IControlPanelSaveHandler<TagControlPanelState>
{
    public async Task<ISaveResult> Save(TagControlPanelState state, CancellationToken cancellationToken)
    {
        if (state.Tag is null)
            throw new InvalidOperationException("No tag provided");

        var tag = new Tag
        {
            Id = state.Tag.Id,
            Text = state.Tag.Text,
            Protected = state.Tag.Protected,
        };

        await connectionService.UpsertTag(tag, cancellationToken);

        return new SaveSuccessResult();
    }
}

