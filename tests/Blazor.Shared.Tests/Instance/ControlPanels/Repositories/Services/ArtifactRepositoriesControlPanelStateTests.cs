using Blazor.Shared.Instance.ControlPanels.Repositories.Models;
using Blazor.Shared.Instance.ControlPanels.Repositories.Services;
using Blazor.Shared.Instance.ControlPanels.Repositories.Extensions;
using Core.Shared.Instance.Contracts;
using Sdk.Messaging;

namespace Blazor.Shared.Tests.Instance.ControlPanels.Repositories.Services;

public sealed class ArtifactRepositoriesControlPanelStateTests
{
    private static ArtifactRepository CreateRepository(string endpoint = "https://repo.example.com")
        => new() { Id = Guid.NewGuid(), Endpoint = endpoint, Name = "Test Repo" };

    private static ArtifactRepositoriesControlPanelState StateWithRepositories(params ArtifactRepository[] repositories)
    {
        var state = new ArtifactRepositoriesControlPanelState();
        state.Repositories = [.. repositories.Select(r => new ArtifactRepositoryModel(r))];
        return state;
    }

    public sealed class UpdateRepositoryTests
    {
        [Fact]
        public void Should_add_repository_to_list_on_created()
        {
            // Arrange
            var repo = CreateRepository();
            var state = new ArtifactRepositoriesControlPanelState();

            // Act
            var result = state.UpdateRepository(repo, CrudAction.Created);

            // Assert
            result.Should().BeTrue();
            state.Repositories.Should().ContainSingle(r => r.Id == repo.Id);
        }

        [Fact]
        public void Should_update_existing_repository_in_list_on_updated()
        {
            // Arrange
            var repo = CreateRepository();
            var state = StateWithRepositories(repo);

            var updatedRepo = new ArtifactRepository { Id = repo.Id, Endpoint = "https://updated.example.com", Name = "Updated Repo" };

            // Act
            var result = state.UpdateRepository(updatedRepo, CrudAction.Updated);

            // Assert
            result.Should().BeTrue();
            state.Repositories.Should().ContainSingle(r => r.Id == repo.Id);
            state.Repositories.Single(r => r.Id == repo.Id).Endpoint.Should().Be(updatedRepo.Endpoint);
        }

        [Fact]
        public void Should_return_false_when_repository_not_found_on_updated()
        {
            // Arrange
            var state = new ArtifactRepositoriesControlPanelState();
            var repo = CreateRepository();

            // Act
            var result = state.UpdateRepository(repo, CrudAction.Updated);

            // Assert
            result.Should().BeFalse();
            state.Repositories.Should().BeEmpty();
        }

        [Fact]
        public void Should_remove_repository_from_list_on_deleted()
        {
            // Arrange
            var repo = CreateRepository();
            var state = StateWithRepositories(repo);

            // Act
            var result = state.UpdateRepository(repo, CrudAction.Deleted);

            // Assert
            result.Should().BeTrue();
            state.Repositories.Should().NotContain(r => r.Id == repo.Id);
        }

        [Fact]
        public void Should_return_false_when_repository_not_found_on_deleted()
        {
            // Arrange
            var state = new ArtifactRepositoriesControlPanelState();
            var repo = CreateRepository();

            // Act
            var result = state.UpdateRepository(repo, CrudAction.Deleted);

            // Assert
            result.Should().BeFalse();
        }
    }

    public sealed class DeleteRepositoryTests
    {
        [Fact]
        public void Should_move_repository_to_marked_for_deletion_and_remove_from_list()
        {
            // Arrange
            var repo = CreateRepository();
            var state = StateWithRepositories(repo);

            // Act
            var result = state.DeleteRepository(repo.Id);

            // Assert
            result.Should().BeTrue();
            state.Repositories.Should().NotContain(r => r.Id == repo.Id);
            state.RepositoriesMarkedForDeletion.Should().ContainSingle(r => r.Id == repo.Id);
        }

        [Fact]
        public void Should_return_false_when_repository_not_found()
        {
            // Arrange
            var state = new ArtifactRepositoriesControlPanelState();

            // Act
            var result = state.DeleteRepository(Guid.NewGuid());

            // Assert
            result.Should().BeFalse();
            state.RepositoriesMarkedForDeletion.Should().BeEmpty();
        }

        [Fact]
        public void Should_not_affect_other_repositories_when_deleting()
        {
            // Arrange
            var repoToDelete = CreateRepository("https://delete.example.com");
            var repoToKeep = CreateRepository("https://keep.example.com");
            var state = StateWithRepositories(repoToDelete, repoToKeep);

            // Act
            state.DeleteRepository(repoToDelete.Id);

            // Assert
            state.Repositories.Should().ContainSingle(r => r.Id == repoToKeep.Id);
            state.RepositoriesMarkedForDeletion.Should().ContainSingle(r => r.Id == repoToDelete.Id);
        }
    }
}
