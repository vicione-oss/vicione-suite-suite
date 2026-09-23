namespace Core.OS.Instance.Extensions;

internal static partial class IArtifactRepositoryStoreExtensions
{
    public static async Task UpdateRepositoryTokens(this IArtifactRepositoryStore repositoryStore, IArtifactRepositoryTokenService tokenService, ILogger logger, CancellationToken cancellationToken)
    {
        // Tokens are refreshed for every enabled repository that has a token endpoint.
        var repositories = await repositoryStore.GetRepositories(null, cancellationToken);
        var updateTasks = repositories.Where(r => r.Enabled && !string.IsNullOrEmpty(r.TokenEndpoint) &&
            (r.TokenValidUntil is null || r.TokenValidUntil < DateTime.UtcNow))
            .Select(async repo =>
            {
                try
                {
                    var tokenResponse = await tokenService.GetToken(new Uri(repo.TokenEndpoint!), cancellationToken);
                    repo.Password = tokenResponse.Token;
                    repo.TokenValidUntil = tokenResponse.ValidUntil;
                    repo.Modified = DateTimeOffset.UtcNow;
                    repo.ModifiedBy = Environment.UserName;

                    LogUpdatedRepositoryToken(logger, repo.Name, repo.TokenValidUntil);
                }
                catch (Exception ex)
                {
                    LogFailedToGetToken(logger, ex, repo.Name, repo.TokenEndpoint);
                }
            }).ToArray();

        if (updateTasks.Length == 0)
        {
            LogSkipUpdateRepositoryTokens(logger);
            return;
        }

        await Task.WhenAll(updateTasks);

        await repositoryStore.Store(repositories, cancellationToken);
    }

    [LoggerMessage(LogLevel.Debug, "Updated repository '{Name}' token, now valid until {ValidUntil}")]
    static partial void LogUpdatedRepositoryToken(ILogger logger, string? name, DateTimeOffset? validUntil);

    [LoggerMessage(LogLevel.Debug, "Skip update repository tokens")]
    static partial void LogSkipUpdateRepositoryTokens(ILogger logger);

    [LoggerMessage(LogLevel.Error, "Failed to get token for repository '{Name}' from '{Endpoint}'")]
    static partial void LogFailedToGetToken(ILogger logger, Exception ex, string? name, string? endpoint);
}
