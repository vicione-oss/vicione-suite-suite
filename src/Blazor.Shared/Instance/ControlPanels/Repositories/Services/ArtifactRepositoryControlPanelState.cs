using Blazor.Shared.Instance.ControlPanels.Repositories.Models;
using Sdk.Client.ControlPanels.Services;

namespace Blazor.Shared.Instance.ControlPanels.Repositories.Services;

public sealed class ArtifactRepositoryControlPanelState : ControlPanelState
{
    /// <remarks>
    /// <see cref="ArtifactRepositoryControlPanel"/> will use this property to decide whether a repository source should be edited (value is set) or created (value is null)
    /// </remarks>
    public Guid? RepositoryId
    {
        get;
        set
        {
            if (value != field)
            {
                field = value;

                OnPropertyChanged();
            }
        }
    }

    public ArtifactRepositoryModel? Repository { get; internal set; }

    internal bool IsEditMode => RepositoryId.HasValue;
}
