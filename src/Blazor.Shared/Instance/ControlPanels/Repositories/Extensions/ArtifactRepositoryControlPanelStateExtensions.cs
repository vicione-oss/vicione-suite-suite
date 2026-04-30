using Blazor.Shared.Instance.ControlPanels.Repositories.Services;
using Core.Shared.Instance.Contracts;

namespace Blazor.Shared.Instance.ControlPanels.Repositories.Extensions;

internal static class ArtifactRepositoryControlPanelStateExtensions
{
    internal static void UpdateRepository(this ArtifactRepositoryControlPanelState state, ArtifactRepository source)
    {
        ArgumentNullException.ThrowIfNull(state.Repository);

        state.Repository.Update(source);
    }
}
