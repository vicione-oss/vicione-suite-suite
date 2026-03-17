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

    internal bool UpdateRepository(ArtifactRepository source, CrudAction action)
    {
        if (action == CrudAction.Created)
        {
            Repositories.Add(new ArtifactRepositoryModel(source));
            return true;
        }

        if (action == CrudAction.Updated)
        {
            var repositoryToUpdate = Repositories.FirstOrDefault(s => s.Id == source.Id);
            if (repositoryToUpdate is not null)
            {
                repositoryToUpdate.Update(source);
                return true;
            }
        }
        else if (action == CrudAction.Deleted)
        {
            var repositoryToRemove = Repositories.FirstOrDefault(s => s.Id == source.Id);
            if (repositoryToRemove is not null)
            {
                Repositories.Remove(repositoryToRemove);
                return true;
            }
        }

        return false;
    }

    internal bool DeleteRepository(Guid repositoryId)
    {
        var repository = Repositories.FirstOrDefault(r => r.Id == repositoryId);
        if (repository is null)
            return false;

        RepositoriesMarkedForDeletion.Add(repository);
        Repositories.Remove(repository);
        return true;
    }
}
