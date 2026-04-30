using Blazor.Shared.Instance.ControlPanels.Repositories.Models;
using Core.Shared.Instance.Contracts;
using Sdk.Client.ControlPanels.Services;
using Sdk.Messaging;

namespace Blazor.Shared.Instance.ControlPanels.Repositories.Services;

public sealed class ArtifactRepositoriesControlPanelState : ControlPanelState
{
    public string? FilterText { get; set; } = string.Empty;

    internal List<ArtifactRepositoryModel> Repositories
    {
        get;
        set
        {
            if (value == field)
                return;

            field = value;

            OnPropertyChanged(nameof(Repositories));
        }
    } = [];

    public int RequestErrorCode
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

    public string? RequestErrorMessage
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

    public List<ArtifactRepositoryModel> RepositoriesMarkedForDeletion { get; } = [];
}
