using Blazor.Shared.Instance.ControlPanels.Repositories.Models;
using Blazor.Shared.Instance.ControlPanels.Repositories.Services;
using Core.Shared.Instance.Contracts;
using Sdk.Messaging;

namespace Blazor.Shared.Instance.ControlPanels.Repositories.Extensions;

internal static class ArtifactRepositoriesControlPanelStateExtensions
{
    extension(ArtifactRepositoriesControlPanelState state)
    {
        internal bool UpdateRepository(ArtifactRepository source, CrudAction action)
        {
            if (action == CrudAction.Created)
            {
                state.Repositories.Add(new ArtifactRepositoryModel(source));
                return true;
            }

            if (action == CrudAction.Updated)
            {
                var repositoryToUpdate = state.Repositories.FirstOrDefault(s => s.Id == source.Id);
                if (repositoryToUpdate is not null)
                {
                    repositoryToUpdate.Update(source);
                    return true;
                }
            }
            else if (action == CrudAction.Deleted)
            {
                var repositoryToRemove = state.Repositories.FirstOrDefault(s => s.Id == source.Id);
                if (repositoryToRemove is not null)
                {
                    state.Repositories.Remove(repositoryToRemove);
                    return true;
                }
            }

            return false;
        }

        internal bool DeleteRepository(Guid repositoryId)
        {
            var repository = state.Repositories.FirstOrDefault(r => r.Id == repositoryId);
            if (repository is null)
                return false;

            state.RepositoriesMarkedForDeletion.Add(repository);
            state.Repositories.Remove(repository);
            return true;
        }
    }
}
