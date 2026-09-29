using Core.OS.DbContext;
using Core.Shared.Extensions;
using Core.Shared.UserManagement.Commands;
using Core.Shared.UserManagement.Configuration;
using Core.Shared.UserManagement.Events;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Sdk.Messaging;
using ExternalIdProviderEntity = Core.Shared.UserManagement.Contracts.ExternalIdProvider;

namespace Core.OS.UserManagement.Consumers;

/// <summary>
/// Stores or removes the single OpenID provider and unlinks the external logins that a changed
/// identity domain invalidates.
/// </summary>
/// <remarks>
/// Unlinks before writing, so a redelivery still compares against the old row and repeats the unlink.
/// Unexpected failures are reported by <see cref="SetExternalIdProviderFaultConsumer"/> (ADR-004 D6).
/// </remarks>
public sealed partial class SetExternalIdProviderConsumer(
    ApplicationDbContext applicationDbContext,
    UserDbContext userDbContext,
    IConfiguration configuration,
    ILogger<SetExternalIdProviderConsumer> logger)
    : IConsumer<SetExternalIdProvider>
{
    /// <summary>
    /// Decides whether existing external logins still belong to the provider.
    /// <see langword="null"/> means no provider is in effect.
    /// </summary>
    private sealed record IdentityDomain(string Authority, string ClientId)
    {
        public string Authority { get; } = Canonical(Authority);

        /// <summary>
        /// The OpenID handler adds a missing trailing slash before the discovery path, so both spellings name one provider.
        /// </summary>
        private static string Canonical(string authority)
            => Uri.TryCreate(authority, UriKind.Absolute, out var uri)
                ? uri.GetLeftPart(UriPartial.Path).TrimEnd('/')
                : authority;
    }

    public async Task Consume(ConsumeContext<SetExternalIdProvider> context)
    {
        var correlationId = context.Message.CorrelationId;

        LogConsume(logger, correlationId);

        var authority = context.Message.Authority.Trim();
        var clientId = context.Message.ClientId.Trim();

        var validationError = Validate(authority, clientId);
        if (validationError is not null)
        {
            LogRejected(logger, correlationId, validationError);

            await context.Publish(
                new SetExternalIdProviderError(correlationId,
                    new ErrorInfo(UserErrorCodes.ExternalIdProviderInvalid, validationError)),
                context.CancellationToken);

            return;
        }

        var stored = await applicationDbContext.ExternalIdProviders.FirstOrDefaultAsync(context.CancellationToken);
        var removal = authority.Length == 0;

        if (IdentityDomainChanges(stored, removal, authority, clientId))
            await UnlinkAllExternalLogins(context.CancellationToken);

        if (removal)
            RemoveProvider(stored);
        else
            StoreProvider(stored, authority, clientId, context.Message.ClientSecret);

        await applicationDbContext.SaveChangesAsync(context.CancellationToken);

        await context.Publish(new ExternalIdProviderChanged(correlationId), context.CancellationToken);
    }

    private static string? Validate(string authority, string clientId)
    {
        if (authority.Length == 0 && clientId.Length == 0)
            return null;

        if (authority.Length == 0 || clientId.Length == 0)
            return "An OpenID provider needs both an authority and a client id; removing it needs neither.";

        return ExternalIdProviderValidation.IsValidAuthority(authority)
            ? null
            : "The authority must be an absolute https URL.";
    }

    /// <summary>
    /// Resolves both sides like <see cref="Configuration.DynamicExternalIdProviderOptions"/> (stored row,
    /// then file), so adopting an unchanged file-based provider into the panel unlinks no one.
    /// </summary>
    private bool IdentityDomainChanges(ExternalIdProviderEntity? stored, bool removal, string authority,
        string clientId)
    {
        var before = Domain(stored) ?? ConfiguredDomain();
        var after = removal ? ConfiguredDomain() : new IdentityDomain(authority, clientId);

        return before != after;
    }

    private static IdentityDomain? Domain(ExternalIdProviderEntity? provider)
        => provider is null ? null : new IdentityDomain(provider.Authority, provider.ClientId);

    private IdentityDomain? ConfiguredDomain()
    {
        var configured = configuration.GetExternalIdProviderOptions()?.Providers.FirstOrDefault();

        return configured is null ? null : new IdentityDomain(configured.Authority, configured.ClientId);
    }

    private async Task UnlinkAllExternalLogins(CancellationToken cancellationToken)
    {
        var logins = await userDbContext.UserLogins
            .Where(l => l.LoginProvider == ProviderConstants.DefaultProviderName)
            .ToListAsync(cancellationToken);

        if (logins.Count == 0)
            return;

        userDbContext.UserLogins.RemoveRange(logins);

        await userDbContext.SaveChangesAsync(cancellationToken);
    }

    private void RemoveProvider(ExternalIdProviderEntity? stored)
    {
        if (stored is not null)
            applicationDbContext.ExternalIdProviders.Remove(stored);
    }

    private void StoreProvider(ExternalIdProviderEntity? stored, string authority, string clientId,
        ClientSecretUpdate secretUpdate)
    {
        if (stored is null)
        {
            applicationDbContext.ExternalIdProviders.Add(new ExternalIdProviderEntity
            {
                Name = ProviderConstants.DefaultProviderName,
                Authority = authority,
                ClientId = clientId,
                ClientSecret = ApplySecretUpdate(null, secretUpdate)
            });

            return;
        }

        stored.Authority = authority;
        stored.ClientId = clientId;
        stored.ClientSecret = ApplySecretUpdate(stored.ClientSecret, secretUpdate);
    }

    private static string? ApplySecretUpdate(string? storedSecret, ClientSecretUpdate update)
        => update.Kind switch
        {
            ClientSecretUpdateKind.Set => update.Value,
            ClientSecretUpdateKind.Clear => null,
            _ => storedSecret
        };

    [LoggerMessage(LogLevel.Debug, "Consuming set external id provider command correlated by {CorrelationId}.")]
    private static partial void LogConsume(ILogger<SetExternalIdProviderConsumer> logger, Guid correlationId);

    [LoggerMessage(LogLevel.Warning, "Rejected the set external id provider command correlated by {CorrelationId}: {Reason}")]
    private static partial void LogRejected(ILogger<SetExternalIdProviderConsumer> logger, Guid correlationId, string reason);
}
