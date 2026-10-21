using Blazor.Shared.Instance.ControlPanels.Repositories.Models;
using Sdk.Client.ControlPanels.Services;
using ViciOne.SourceGenerators.NotifyChanged;

namespace Blazor.Shared.Instance.ControlPanels.Repositories.Services;

public sealed partial class ArtifactRepositoryControlPanelState : ControlPanelState
{
    /// <remarks>
    /// <see cref="ArtifactRepositoryControlPanel"/> will use this property to decide whether a repository source should be edited (value is set) or created (value is null)
    /// </remarks>
    [NotifyChanged]
    public partial Guid? RepositoryId { get; set; }

    public ArtifactRepositoryModel? Repository { get; internal set; }

    internal bool IsEditMode => RepositoryId.HasValue;
}
