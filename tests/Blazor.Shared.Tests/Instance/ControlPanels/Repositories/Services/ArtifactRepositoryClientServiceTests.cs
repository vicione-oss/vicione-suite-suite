using AwesomeAssertions;
using Blazor.Shared.Instance.ControlPanels.Repositories.Models;
using Blazor.Shared.Instance.ControlPanels.Repositories.Services;
using Core.Shared.Instance.Commands;
using Core.Shared.Instance.Contracts;
using Core.Shared.Instance.Events;
using NSubstitute;
using Sdk.Client.Infrastructure;
using Sdk.Messaging;
using Xunit;

namespace Blazor.Shared.Tests.Instance.ControlPanels.Repositories.Services;

public class ArtifactRepositoryClientServiceTests
{
    private readonly IUiMediator _mediator = Substitute.For<IUiMediator>();

    private static ArtifactRepositoryModel CreateModel()
        => new(new ArtifactRepository { Id = Guid.NewGuid(), Endpoint = "https://repo.example.com", Name = "Test Repo" });

    private static void SetupSendAndSimulateEvent<TCommand>(
        IUiMediator mediator,
        ArtifactRepositoryClientService service,
        Func<TCommand, ArtifactRepositoryChanged> eventFactory)
        where TCommand : class, ICommand
        => mediator.When(m => m.Send(Arg.Any<TCommand>(), Arg.Any<CancellationToken>()))
            .Do(async callinfo =>
            {
                var command = callinfo.Arg<TCommand>();
                var @event = eventFactory(command);
                if (@event is ArtifactRepositoryChanged change)
                {
                    var context = new ClientContext<ArtifactRepositoryChanged>(change, command.CorrelationId);
                    await service.Consume(context, TestContext.Current.CancellationToken);
                }
            });

    public sealed class CreateRepositoryTests : ArtifactRepositoryClientServiceTests
    {
        [Fact]
        public async Task Should_send_create_command_and_return_success()
        {
            // Arrange
            var model = CreateModel();
            using var service = new ArtifactRepositoryClientService(_mediator);

            SetupSendAndSimulateEvent<CreateArtifactRepository>(
                _mediator, service,
                cmd => new ArtifactRepositoryChanged(cmd.Repository, CrudAction.Created) { CorrelationId = cmd.CorrelationId });

            // Act
            var result = await service.CreateRepository(model, TestContext.Current.CancellationToken);

            // Assert
            result.Should().BeOfType<ArtifactRepositoryServiceSuccessResult>();
            await _mediator.Received(1).Send(Arg.Any<CreateArtifactRepository>(), Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task Should_return_error_when_backend_reports_error()
        {
            // Arrange
            var model = CreateModel();
            using var service = new ArtifactRepositoryClientService(_mediator);

            SetupSendAndSimulateEvent<CreateArtifactRepository>(
                _mediator, service,
                cmd => new ArtifactRepositoryChanged(cmd.Repository, CrudAction.Created, new ErrorInfo(100, "create failed"))
                { CorrelationId = cmd.CorrelationId });

            // Act
            var result = await service.CreateRepository(model, TestContext.Current.CancellationToken);

            // Assert
            var errorResult = result.Should().BeOfType<ArtifactRepositoryServiceErrorResult>().Subject;
            errorResult.ErrorMessage.Should().Be("create failed");
        }

        [Fact]
        public async Task Should_fire_repository_changed_event_on_success()
        {
            // Arrange
            var model = CreateModel();
            using var service = new ArtifactRepositoryClientService(_mediator);

            SetupSendAndSimulateEvent<CreateArtifactRepository>(
                _mediator, service,
                cmd => new ArtifactRepositoryChanged(cmd.Repository, CrudAction.Created) { CorrelationId = cmd.CorrelationId });

            ArtifactRepository? changedRepo = null;
            CrudAction? changedAction = null;
            service.RepositoryChanged += (repo, action) => { changedRepo = repo; changedAction = action; return Task.CompletedTask; };

            // Act
            await service.CreateRepository(model, TestContext.Current.CancellationToken);

            // Assert
            changedRepo!.Id.Should().Be(model.Id);
            changedAction.Should().Be(CrudAction.Created);
        }
    }

    public sealed class UpdateRepositoryTests : ArtifactRepositoryClientServiceTests
    {
        [Fact]
        public async Task Should_send_update_command_and_return_success()
        {
            // Arrange
            var model = CreateModel();
            using var service = new ArtifactRepositoryClientService(_mediator);

            SetupSendAndSimulateEvent<UpdateArtifactRepository>(
                _mediator, service,
                cmd => new ArtifactRepositoryChanged(cmd.Repository, CrudAction.Updated) { CorrelationId = cmd.CorrelationId });

            // Act
            var result = await service.UpdateRepository(model, TestContext.Current.CancellationToken);

            // Assert
            result.Should().BeOfType<ArtifactRepositoryServiceSuccessResult>();
            await _mediator.Received(1).Send(Arg.Any<UpdateArtifactRepository>(), Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task Should_return_error_when_backend_reports_error()
        {
            // Arrange
            var model = CreateModel();
            using var service = new ArtifactRepositoryClientService(_mediator);

            SetupSendAndSimulateEvent<UpdateArtifactRepository>(
                _mediator, service,
                cmd => new ArtifactRepositoryChanged(cmd.Repository, CrudAction.Updated, new ErrorInfo(100, "update failed"))
                { CorrelationId = cmd.CorrelationId });

            // Act
            var result = await service.UpdateRepository(model, TestContext.Current.CancellationToken);

            // Assert
            var errorResult = result.Should().BeOfType<ArtifactRepositoryServiceErrorResult>().Subject;
            errorResult.ErrorMessage.Should().Be("update failed");
        }

        [Fact]
        public async Task Should_fire_repository_changed_event_on_success()
        {
            // Arrange
            var model = CreateModel();
            using var service = new ArtifactRepositoryClientService(_mediator);

            SetupSendAndSimulateEvent<UpdateArtifactRepository>(
                _mediator, service,
                cmd => new ArtifactRepositoryChanged(cmd.Repository, CrudAction.Updated));

            ArtifactRepository? changedRepo = null;
            CrudAction? changedAction = null;
            service.RepositoryChanged += (repo, action) => { changedRepo = repo; changedAction = action; return Task.CompletedTask; };

            // Act
            await service.UpdateRepository(model, TestContext.Current.CancellationToken);

            // Assert
            changedRepo!.Id.Should().Be(model.Id);
            changedAction.Should().Be(CrudAction.Updated);
        }
    }

    public sealed class UpdateRepositoryTokenTests : ArtifactRepositoryClientServiceTests
    {
        [Fact]
        public async Task Should_send_update_token_command_and_return_success()
        {
            // Arrange
            var model = CreateModel();
            using var service = new ArtifactRepositoryClientService(_mediator);

            SetupSendAndSimulateEvent<UpdateArtifactRepositoryToken>(
                _mediator, service,
                cmd => new ArtifactRepositoryChanged(cmd.Repository, CrudAction.Updated) { CorrelationId = cmd.CorrelationId });

            // Act
            var result = await service.UpdateRepositoryToken(model, TestContext.Current.CancellationToken);

            // Assert
            result.Should().BeOfType<ArtifactRepositoryServiceSuccessResult>();
            await _mediator.Received(1).Send(Arg.Any<UpdateArtifactRepositoryToken>(), Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task Should_return_error_when_backend_reports_error()
        {
            // Arrange
            var model = CreateModel();
            using var service = new ArtifactRepositoryClientService(_mediator);

            SetupSendAndSimulateEvent<UpdateArtifactRepositoryToken>(
                _mediator, service,
                cmd => new ArtifactRepositoryChanged(cmd.Repository, CrudAction.Updated, new ErrorInfo(100, "token update failed"))
                { CorrelationId = cmd.CorrelationId });

            // Act
            var result = await service.UpdateRepositoryToken(model, TestContext.Current.CancellationToken);

            // Assert
            var errorResult = result.Should().BeOfType<ArtifactRepositoryServiceErrorResult>().Subject;
            errorResult.ErrorMessage.Should().Be("token update failed");
        }
    }

    public sealed class DeleteRepositoryTests : ArtifactRepositoryClientServiceTests
    {
        [Fact]
        public async Task Should_send_delete_command_and_return_success()
        {
            // Arrange
            var model = CreateModel();
            using var service = new ArtifactRepositoryClientService(_mediator);

            SetupSendAndSimulateEvent<DeleteArtifactRepository>(
                _mediator, service,
                cmd => new ArtifactRepositoryChanged(new ArtifactRepository() { Id = cmd.RepositoryId, Endpoint = "null" },
                CrudAction.Deleted)
                { CorrelationId = cmd.CorrelationId });

            // Act
            var result = await service.DeleteRepository(model, TestContext.Current.CancellationToken);

            // Assert
            result.Should().BeOfType<ArtifactRepositoryServiceSuccessResult>();
            await _mediator.Received(1).Send(Arg.Any<DeleteArtifactRepository>(), Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task Should_return_error_when_backend_reports_error()
        {
            // Arrange
            var model = CreateModel();
            using var service = new ArtifactRepositoryClientService(_mediator);

            SetupSendAndSimulateEvent<DeleteArtifactRepository>(
                _mediator, service,
                cmd => new ArtifactRepositoryChanged(new ArtifactRepository() { Id = cmd.RepositoryId, Endpoint = "null" },
                CrudAction.Deleted,
                new ErrorInfo(100, "delete failed"))
                { CorrelationId = cmd.CorrelationId });

            // Act
            var result = await service.DeleteRepository(model, TestContext.Current.CancellationToken);

            // Assert
            var errorResult = result.Should().BeOfType<ArtifactRepositoryServiceErrorResult>().Subject;
            errorResult.ErrorMessage.Should().Be("delete failed");
        }

        [Fact]
        public async Task Should_fire_repository_changed_event_on_success()
        {
            // Arrange
            var model = CreateModel();
            using var service = new ArtifactRepositoryClientService(_mediator);

            SetupSendAndSimulateEvent<DeleteArtifactRepository>(
                _mediator, service,
                cmd => new ArtifactRepositoryChanged(new ArtifactRepository() { Id = cmd.RepositoryId, Endpoint = "null" },
                CrudAction.Deleted)
                { CorrelationId = cmd.CorrelationId });

            ArtifactRepository? changedRepo = null;
            CrudAction? changedAction = null;
            service.RepositoryChanged += (repo, action) => { changedRepo = repo; changedAction = action; return Task.CompletedTask; };

            // Act
            await service.DeleteRepository(model, TestContext.Current.CancellationToken);

            // Assert
            changedRepo!.Id.Should().Be(model.Id);
            changedAction.Should().Be(CrudAction.Deleted);
        }
    }
}
