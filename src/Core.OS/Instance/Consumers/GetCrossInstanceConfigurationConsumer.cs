using Core.OS.DbContext;
using Core.Shared.Instance.Contracts;
using Core.Shared.Instance.Requests;
using Microsoft.EntityFrameworkCore;
using Sdk.Backend.Messaging;

namespace Core.OS.Instance.Consumers;

public sealed partial class GetCrossInstanceConfigurationConsumer(IApplicationDbContext dbContext, ILogger<GetCrossInstanceConfigurationConsumer> logger)
    : RequestConsumer<GetCrossInstanceConfiguration, GetCrossInstanceConfigurationResponse>
{
    public override async Task<GetCrossInstanceConfigurationResponse> Respond(GetCrossInstanceConfiguration message, CancellationToken cancellationToken)
    {
        var crossInstanceConfiguration = await dbContext
            .CrossInstanceConfiguration
            .AsNoTracking()
            .SingleOrDefaultAsync(cancellationToken) ?? new CrossInstanceConfiguration();

        return new GetCrossInstanceConfigurationResponse(crossInstanceConfiguration);
    }

    public override Task<GetCrossInstanceConfigurationResponse> HandleException(GetCrossInstanceConfiguration message, Exception e, CancellationToken cancellationToken)
    {
        LogError(logger, e);

        return Task.FromResult(new GetCrossInstanceConfigurationResponse(new(), new(0, e.Message)));
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to get cross instance configuration")]
    private static partial void LogError(ILogger<GetCrossInstanceConfigurationConsumer> logger, Exception ex);
}
