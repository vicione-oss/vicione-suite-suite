using Core.OS.DbContext;
using Core.Shared.Instance.Contracts;
using Core.Shared.Instance.Requests;
using Microsoft.EntityFrameworkCore;
using Sdk.Backend.Messaging;

namespace Core.OS.Instance.Consumers;

public sealed partial class GetInstancesConsumer(IApplicationDbContext dbContext, ILogger<GetInstancesConsumer> logger) : RequestConsumer<GetInstances, GetInstancesResponse>
{
    public override async Task<GetInstancesResponse> Respond(GetInstances message, CancellationToken cancellationToken)
    {
        List<InstanceInformation>? instanceInfos = null;

        if (message.InstanceId is null)
        {
            instanceInfos = await dbContext
                .InstanceInfo
                .AsNoTracking()
                .ToListAsync(cancellationToken);
        }
        else
        {
            var info = await dbContext
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
        LogError(logger, e);

        return Task.FromResult(new GetInstancesResponse([], new(0, e.Message)));
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to get instances")]
    private static partial void LogError(ILogger<GetInstancesConsumer> logger, Exception ex);
}
