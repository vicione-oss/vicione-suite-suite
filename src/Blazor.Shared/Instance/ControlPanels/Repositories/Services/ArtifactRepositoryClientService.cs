using Blazor.Shared.Instance.ControlPanels.Repositories.Models;
using Core.Shared.Instance.Commands;
using Core.Shared.Instance.Contracts;
using Core.Shared.Instance.Events;
using Sdk.Client.ControlPanels.Services;
using Sdk.Client.Infrastructure;
using Sdk.Messaging;

namespace Blazor.Shared.Instance.ControlPanels.Repositories.Services;

internal sealed class ArtifactRepositoryClientService : CompletionSourceHandlerBase<IArtifactRepositoryServiceResult>,
    IArtifactRepositoryClientService,
    IEventConsumer<ArtifactRepositoryChanged>,
    IDisposable
{
    public event Func<ArtifactRepository, CrudAction, Task>? RepositoryChanged;

    public ArtifactRepositoryClientService(IUiMediator mediator) : base(mediator)
        => Register<ArtifactRepositoryChanged>();

    public async Task<IArtifactRepositoryServiceResult> CreateRepository(ArtifactRepositoryModel sourceModel, CancellationToken cancellationToken = default)
    {
        var command = new CreateArtifactRepository(sourceModel.ToEntity());

        return await SendAndWaitForCompletion(command, cancellationToken);
    }

    public async Task<IArtifactRepositoryServiceResult> UpdateRepository(ArtifactRepositoryModel sourceModel, CancellationToken cancellationToken = default)
    {
        var command = new UpdateArtifactRepository(sourceModel.ToEntity());

        return await SendAndWaitForCompletion(command, cancellationToken);
    }

    public async Task<IArtifactRepositoryServiceResult> UpdateRepositoryToken(ArtifactRepositoryModel sourceModel, CancellationToken cancellationToken = default)
    {
        var command = new UpdateArtifactRepositoryToken(sourceModel.ToEntity());

        return await SendAndWaitForCompletion(command, cancellationToken);
    }

    public async Task<IArtifactRepositoryServiceResult> DeleteRepository(ArtifactRepositoryModel sourceModel, CancellationToken cancellationToken = default)
    {
        var command = new DeleteArtifactRepository(sourceModel.Id);

        return await SendAndWaitForCompletion(command, cancellationToken);
    }

    public async Task Consume(ClientContext<ArtifactRepositoryChanged> context, CancellationToken cancellationToken)
    {
        if (context.Message.Error is not null)
        {
            CompleteWithError(context.Message.CorrelationId, context.Message.Error);
            return;
        }

        try
        {
            await NotifyRepositoryChanged(context.Message.Repository, context.Message.Action);
        }
        finally
        {
            CompleteWithSuccess(context.Message.CorrelationId);
        }
    }

    protected override IArtifactRepositoryServiceResult CreateSuccessResult()
        => new ArtifactRepositoryServiceSuccessResult();

    protected override IArtifactRepositoryServiceResult CreateErrorResult(string errorMessage, int? errorCode = null)
        => new ArtifactRepositoryServiceErrorResult(errorMessage, errorCode);

    private async Task NotifyRepositoryChanged(ArtifactRepository source, CrudAction crudAction)
    {
        if (RepositoryChanged is not null)
            await RepositoryChanged.Invoke(source, crudAction);
    }
}
