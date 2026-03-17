using Blazor.Shared.Instance.ControlPanels.Repositories.Models;
using Core.Shared.Instance.Contracts;
using Sdk.Client.ControlPanels.Services;

namespace Blazor.Shared.Instance.ControlPanels.Repositories.Services;

public sealed class ArtifactRepositoryControlPanelState : ControlPanelState
{
    /// <remarks>
    /// <see cref="ArtifactRepositoryControlPanel"/> will use this property to decide whether a repository source could be edited (value is set) or not (value is null)
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

    public void UpdateRepository(ArtifactRepository source)
    {
        ArgumentNullException.ThrowIfNull(Repository);

        Repository.Update(source);
    }
}
