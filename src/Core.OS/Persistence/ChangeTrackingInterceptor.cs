using System.Collections;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Metadata;
using Sdk.Backend.Messaging;
using Sdk.Backend.Persistence;
using Sdk.Messaging;

namespace Core.OS.Persistence;

public sealed class ChangeTrackingInterceptor(ISuiteMediator mediator, ReplicationSequenceCounter sequenceCounter, TimeProvider timeProvider) : ISaveChangesInterceptor
{
    public ChangeTrackingInterceptor(ISuiteMediator mediator, ReplicationSequenceCounter sequenceCounter)
        : this(mediator, sequenceCounter, TimeProvider.System) { }

    public async ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        if (eventData.Context is not IModuleDbContext context)
            return result;

        var ignoredEntries = new List<EntityEntry>();
        var entities = context.ChangeTracker.Entries().ToArray();
        GetIgnoredEntities(entities, ignoredEntries, context, false, null);

        var changeList = (from entityEntry in entities
                          let entityType = entityEntry.Entity.GetType()
                          where entityEntry.State is not EntityState.Unchanged and not EntityState.Detached
                          select new ChangedEntity(ignoredEntries.Select(e => e.Entity).Contains(entityEntry.Entity)
                                  ? null
                                  : JsonSerializer.Serialize(entityEntry.Entity, DefaultJsonSerializerSettings.Default),
                              entityType.FullName ?? entityType.Name,
                              entityType.Assembly.FullName,
                              entityEntry.State)).ToList();

        var publishableChanges = changeList.Where(c => c.Entity is not null).ToList();
        if (publishableChanges.Count == 0)
            return result;

        Type? contextType = null;
        foreach (var interfaceType in eventData.Context.GetType().GetInterfaces())
        {
            var innerInterface = interfaceType.GetInterfaces();
            if (innerInterface.Contains(typeof(IModuleDbContext))
                && !innerInterface.Contains(typeof(ISqliteDbContext))
                && !innerInterface.Contains(typeof(IPostgresDbContext)))
            {
                contextType = interfaceType;
                break;
            }
        }

        if (contextType is null)
            return result;

        // Publish within SavingChangesAsync so the Bus Outbox captures the message
        // atomically. When the Bus Outbox is active (master + RabbitMQ), the message
        // is written to the outbox table and delivered asynchronously by the delivery
        // service. When using in-memory bus (standalone), the publish goes directly.
        var contextTypeName = contextType.FullName ?? contextType.Name;
        var sequenceNumber = sequenceCounter.Next(contextTypeName);

        // Persist counter to outbox schema BEFORE publishing. This ensures the counter
        // is always >= any delivered sequence number, even after master crash (ADR-003 Gap 4).
        // If the subsequent business transaction fails, the counter is ahead by 1 (harmless —
        // slaves handle gaps via the reorder buffer and timeout-triggered resync).
        await PersistSequenceNumber(eventData.Context, contextTypeName, sequenceNumber, cancellationToken);

        var message = new DbChangeSet(publishableChanges, contextTypeName, sequenceNumber, timeProvider.GetUtcNow());
        await mediator.Publish(message, cancellationToken);

        return result;
    }

    private static async Task PersistSequenceNumber(
        Microsoft.EntityFrameworkCore.DbContext dbContext,
        string contextType,
        long sequenceNumber,
        CancellationToken cancellationToken)
    {
        // Only persist on PostgreSQL (master). Only module DbContexts implementing IPostgresDbContext
        // are synchronized, and those run on PostgreSQL on the master.
        if (dbContext is not IPostgresDbContext)
            return;

        // Uses the same database connection as the module's DbContext. Executes in auto-commit
        // mode (outside the business transaction). On PostgreSQL, this is an atomic upsert.
        await dbContext.Database.ExecuteSqlRawAsync("""
            INSERT INTO outbox."ReplicationSequenceState" ("ContextType", "LastSequenceNumber")
            VALUES ({0}, {1})
            ON CONFLICT ("ContextType") DO UPDATE SET "LastSequenceNumber" = {1}
            """, [contextType, sequenceNumber], cancellationToken);
    }

    public ValueTask<int> SavedChangesAsync(
        SaveChangesCompletedEventData eventData,
        int result,
        CancellationToken cancellationToken = default)
        => new(result);

    public Task SaveChangesFailedAsync(DbContextErrorEventData eventData, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public Task SaveChangesCanceledAsync(DbContextEventData eventData, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
        => throw new InvalidOperationException("Do not save changes synchronously. Use 'SaveChangesAsync' instead!");

    // Filters out all EntityEntries that are either blacklisted by the context, or relations of other EntityEntries
    private static void GetIgnoredEntities(IEnumerable<EntityEntry> entries, ICollection<EntityEntry> ignoredEntities, IModuleDbContext dbContext, bool isRelation, HashSet<string>? processedNavigations)
    {
        processedNavigations ??= [];

        foreach (var entry in entries)
        {
            // Add blacklisted entities and all relations
            if (isRelation || dbContext.NotSynchronizedEntityTypes.Contains(entry.Entity.GetType()))
                ignoredEntities.Add(entry);

            foreach (var navigation in entry.Navigations)
            {
                if (!processedNavigations.Add($"{entry.Metadata.Name}|{navigation.Metadata.Name}"))
                    continue;

                if (navigation.CurrentValue is IEnumerable navigationCollection)
                {
                    // skip unidirectional relations like tag -> connection
                    if (navigation.Metadata is RuntimeSkipNavigation rt && !rt.IsLeftNavigation())
                        continue;

                    foreach (var relatedEntity in navigationCollection)
                    {
                        var relatedEntityEntry = ((Microsoft.EntityFrameworkCore.DbContext)dbContext).Entry(relatedEntity);
                        GetIgnoredEntities([relatedEntityEntry], ignoredEntities, dbContext, true, processedNavigations);
                    }
                    continue;
                }

                if (navigation.CurrentValue is { } entity)
                {
                    var relatedEntityEntry = ((Microsoft.EntityFrameworkCore.DbContext)dbContext).Entry(entity);
                    GetIgnoredEntities([relatedEntityEntry], ignoredEntities, dbContext, true, processedNavigations);
                }
            }
        }
    }
}
