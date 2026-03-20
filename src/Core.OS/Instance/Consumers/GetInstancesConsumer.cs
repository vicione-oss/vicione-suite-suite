using Core.OS.DbContext;
using Core.Shared.Instance.Contracts;
using Core.Shared.Instance.Requests;
using Microsoft.EntityFrameworkCore;
using Sdk.Backend.Messaging;

namespace Core.OS.Instance.Consumers;

public sealed class GetInstancesConsumer(IApplicationDbContext dbContext, ILogger<GetInstancesConsumer> logger) : RequestConsumer<GetInstances, GetInstancesResponse>
{
    private readonly IApplicationDbContext _dbContext = dbContext;
    private readonly ILogger<GetInstancesConsumer> _logger = logger;

    public override async Task<GetInstancesResponse> Respond(GetInstances message, CancellationToken cancellationToken)
    {
        List<InstanceInformation>? instanceInfos = null;

        if (message.InstanceId is null)
        {
            instanceInfos = await _dbContext
                .InstanceInfo
                .AsNoTracking()
                .ToListAsync(cancellationToken);
        }
        else
        {
            var info = await _dbContext
                .InstanceInfo
                .AsNoTracking()
                .SingleOrDefaultAsync(i => i.Id == message.InstanceId, cancellationToken);
            if (info is not null)
                instanceInfos = [info];
        }

        return new GetInstancesResponse(instanceInfos ?? []);
    }

    public override Task<GetInstancesResponse> HandleException(GetInstances message, Exception e, CancellationToken cancellationToken)
    {
        _logger.LogError(e, $"Failed to handle {nameof(GetInstances)}");

        return Task.FromResult(new GetInstancesResponse([], new(0, e.Message)));
    }
}
