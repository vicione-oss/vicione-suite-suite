using System.Text;
using Core.OS.Instance.Contracts;
using Core.OS.Instance.Services;
using Core.OS.Persistence;
using Core.OS.Persistence.Extensions;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Sdk.Backend.Persistence;

namespace Core.OS.Instance.Initialization;

public sealed class SyncDataActivity(IServiceProvider services, ILogger<SyncDataActivity> logger) : IExecuteActivity<SyncDataArguments>
{
    private readonly IServiceProvider _services = services;
    private readonly ILogger<SyncDataActivity> _logger = logger;

    public async Task<ExecutionResult> Execute(ExecuteContext<SyncDataArguments> context)
    {
        if (context.Arguments.SyncCompleted)
        {
            _logger.LogDebug("Initial Sync finished. Allowing regular message processing");
            _services.GetRequiredService<ReplicationSequenceTracker>().Reset();
            _services.GetRequiredService<SyncRetryState>().Reset();
            _services.NotifyReplicationObservers(_logger, observer => observer.Resynchronized());
            _services.GetRequiredService<SynchronizationState>().CompleteSynchronization();
            return context.Completed();
        }

        if (context.Arguments.ValuesCsv.Count == 0) //should not be send in the first place, but we don't want to assume
            return context.Completed();

        IEnumerable<ModuleContextTypeInformation> allContextTypeInfos;
        try
        {
            allContextTypeInfos = _services.GetServices<ModuleContextTypeInformation>();
        }
        catch (InvalidOperationException)
        {
            return context.Completed(); //nothing to do (no registered Context - can happen in unit testing
        }

        var contextType = allContextTypeInfos
            .Where(c => c.ModuleId == context.Arguments.ModuleId)
            .Select(i => i.ContextType)
            .FirstOrDefault(c => (c.AssemblyQualifiedName ?? c.Name) == context.Arguments.DbContextTypeName) ??
            throw new TypeLoadException($"Context type '{context.Arguments.DbContextTypeName}' is not loaded");

        _logger.LogDebug("Executing context operations for {Module}.{Table} (rows:{Count})",
            context.Arguments.ModuleId, context.Arguments.Table, context.Arguments.ValuesCsv.Count);

        using var scope = _services.CreateScope();
        var dbContext = (IModuleDbContext)scope.ServiceProvider.GetRequiredService(contextType);

        await using var conn = dbContext.Database.GetDbConnection();
        await using var cmd = conn.CreateCommand();
        await conn.OpenAsync(context.CancellationToken);

        cmd.CommandText = "PRAGMA foreign_keys = OFF";
        await cmd.ExecuteNonQueryAsync(context.CancellationToken).ConfigureAwait(false);

        cmd.CommandText = $"DELETE FROM {context.Arguments.Table}";
        await cmd.ExecuteNonQueryAsync(context.CancellationToken).ConfigureAwait(false);

        cmd.CommandText = ToSqliteInsert(context.Arguments);
        await cmd.ExecuteNonQueryAsync(context.CancellationToken).ConfigureAwait(false);

        cmd.CommandText = "PRAGMA foreign_keys = ON";
        await cmd.ExecuteNonQueryAsync(context.CancellationToken).ConfigureAwait(false);

        return context.Completed();
    }

    private static string ToSqliteInsert(SyncDataArguments msg)
    {
        var sb = new StringBuilder("INSERT INTO ");
        sb.Append(msg.Table).Append(" (").Append(msg.ColumnsCsv).Append(')').AppendLine();
        sb.Append("VALUES ");
        for (var i = 0; i < msg.ValuesCsv.Count; i++)
        {
            var valueLine = msg.ValuesCsv[i];
            sb.Append('(').Append(valueLine).Append(')');
            if (i != msg.ValuesCsv.Count - 1) //not on last iteration
                sb.AppendLine(",");
        }

        return sb.ToString();
    }
}
