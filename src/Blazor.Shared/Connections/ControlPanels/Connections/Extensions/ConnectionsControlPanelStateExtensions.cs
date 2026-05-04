using System.ComponentModel;
using Blazor.Shared.Connections.ControlPanels.Connections.Services;
using Sdk.Connections.Contracts;
using Sdk.Connections.Events;
using Sdk.Messaging;

namespace Blazor.Shared.Connections.ControlPanels.Connections.Extensions;

internal static class ConnectionsControlPanelStateExtensions
{
    internal static void UpdateTags(this ConnectionsControlPanelState state, TagsChanged changeEvent)
    {
        var tags = new Dictionary<Guid, Tag>(state.Tags);

        foreach (var tag in changeEvent.Tags)
        {
            switch (changeEvent.Action)
            {
                case CrudAction.Created:
                case CrudAction.Updated:
                    tags[tag.Id] = tag;
                    break;
                case CrudAction.Deleted:
                    break;
                default:
                    throw new InvalidEnumArgumentException(nameof(changeEvent.Action), (int)changeEvent.Action, typeof(CrudAction));
            }
        }

        state.Tags = tags;
    }
}
