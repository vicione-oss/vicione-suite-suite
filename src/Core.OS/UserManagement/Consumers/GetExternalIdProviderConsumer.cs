using Core.OS.DbContext;
using Core.Shared.UserManagement.Events;
using Core.Shared.UserManagement.Requests;
using Microsoft.EntityFrameworkCore;
using Sdk.Backend.Messaging;
using Sdk.Messaging;

namespace Core.OS.UserManagement.Consumers;

public sealed partial class GetExternalIdProviderConsumer(
    ApplicationDbContext dbContext,
    ILogger<GetExternalIdProviderConsumer> logger)
    : RequestConsumer<GetExternalIdProvider, GetExternalIdProviderResponse>
{
    private static readonly GetExternalIdProviderResponse _unconfigured
        = new(string.Empty, string.Empty, ClientSecretStored: false);

    public override async Task<GetExternalIdProviderResponse> Respond(GetExternalIdProvider message,
        CancellationToken cancellationToken)
    {
        var provider = await dbContext.ExternalIdProviders.AsNoTracking()
            .FirstOrDefaultAsync(cancellationToken);

        return provider is null
            ? _unconfigured
            : new GetExternalIdProviderResponse(provider.Authority,
                provider.ClientId,
                !string.IsNullOrEmpty(provider.ClientSecret));
    }

    public override Task<GetExternalIdProviderResponse> HandleException(GetExternalIdProvider message, Exception e,
        CancellationToken cancellationToken)
    {
        LogRequestError(logger, e);

        return Task.FromResult(_unconfigured with
        {
            RequestError = new ErrorInfo(UserErrorCodes.UnknownError, e.Message)
        });
    }

    [LoggerMessage(LogLevel.Error, "Failed to read the external id provider")]
    private static partial void LogRequestError(ILogger<GetExternalIdProviderConsumer> logger, Exception exception);
}
