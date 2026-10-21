using Blazor.Shared.Instance.ControlPanels.Repositories.Models;
using Sdk.Client.ControlPanels.Services;
using ViciOne.SourceGenerators.NotifyChanged;

namespace Blazor.Shared.Instance.ControlPanels.Repositories.Services;

public sealed partial class ArtifactRepositoriesControlPanelState : ControlPanelState
{
    public string? FilterText { get; set; } = string.Empty;

    [NotifyChanged]
    internal partial List<ArtifactRepositoryModel> Repositories { get; set; } = [];

    [NotifyChanged]
    public partial int RequestErrorCode { get; set; }

    [NotifyChanged]
    public partial string? RequestErrorMessage { get; set; }

    public List<ArtifactRepositoryModel> RepositoriesMarkedForDeletion { get; } = [];
}
