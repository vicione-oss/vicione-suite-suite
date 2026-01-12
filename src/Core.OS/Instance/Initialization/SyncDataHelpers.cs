using System.Data.Common;
using Core.OS.DbContext;
using Microsoft.EntityFrameworkCore;
using Sdk.Backend.Modules;
using Sdk.Backend.Persistence;

namespace Core.OS.Instance.Initialization;

internal static class SyncDataHelpers
{
    public const string InstanceCommandKey = "Cmd";
    public const string InstanceIdVariableKey = "InstanceId";
    public const string InstanceIsNewVariableKey = "IsNew";

    private static ModuleContextTypeInformation[] TryGetContextTypeInfos(IServiceProvider services)
    {
        try
        {
            return [.. services.GetServices<ModuleContextTypeInformation>()];
        }
        catch (InvalidOperationException) //can happen in unit testing
        {
            return [];
        }
    }

    public static async Task<IEnumerable<SyncDataArguments>> CreateSyncDataArgumentsPg(IServiceProvider services,
        IEnumerable<string> moduleIds,
        CancellationToken cancellationToken)
    {
        var contextTypeInfos = TryGetContextTypeInfos(services);
        var list = new List<SyncDataArguments>();

        var dataSyncArgs = new FunctionParameters(
            async (context, command) =>
            {
                var schema = context.DefaultSchemaName;
                return await command.GetTablesPg(schema, cancellationToken);
            },
            async (context, command, table) =>
            {
                var schema = context.DefaultSchemaName;
                return await command.GetColumnsCsvPg(schema, table, cancellationToken);
            },
            async (context, command, table, columns) =>
            {
                var schema = context.DefaultSchemaName;
                return await command.GetValuesCsvPg(schema, table, columns, cancellationToken);
            }
        );

        foreach (var moduleId in moduleIds)
        {
            var arguments = await CreateSyncDataArguments(services, moduleId, contextTypeInfos, dataSyncArgs, cancellationToken);
            list.AddRange(arguments);
        }

        return list;
    }

    public static async Task<IEnumerable<SyncDataArguments>> CreateSyncDataArgumentsSqlite(IServiceProvider services,
        IEnumerable<string> moduleIds,
        CancellationToken cancellationToken)
    {
        var contextTypeInfos = TryGetContextTypeInfos(services);
        var list = new List<SyncDataArguments>();

        var dataSyncArgs = new FunctionParameters(
            async (_, command) =>
                await command.GetTablesSqlite(cancellationToken),
            async (_, command, table) =>
                await command.GetColumnsCsvSqlite(table, cancellationToken),
            async (_, command, table, columns) =>
                await command.GetValuesCsvSqlite(table, columns, cancellationToken)
        );

        foreach (var moduleId in moduleIds)
        {
            var arguments = await CreateSyncDataArguments(services, moduleId, contextTypeInfos, dataSyncArgs, cancellationToken);
            list.AddRange(arguments);
        }

        return list;
    }

    private static async Task<IEnumerable<SyncDataArguments>> CreateSyncDataArguments(IServiceProvider services, string moduleId,
        ModuleContextTypeInformation[]? contextTypeInfos, FunctionParameters dataSyncArgs, CancellationToken cancellationToken)
    {
        contextTypeInfos ??= TryGetContextTypeInfos(services);

        var list = new List<SyncDataArguments>();

        foreach (var (contextModule, contextType, _) in contextTypeInfos)
        {
            if (contextModule != moduleId || services.GetService(contextType) is not IModuleDbContext context)
                continue;

            var connection = context.Instance.Database.GetDbConnection();
            await connection.OpenAsync(cancellationToken);
            await using var command = connection.CreateCommand();

            var tableList = await dataSyncArgs.GetTables(context, command).ConfigureAwait(false);

            foreach (var table in tableList)
            {
                var columnsCsvInQuotes = await dataSyncArgs.GetColumnsCsv(context, command, table).ConfigureAwait(false);
                var valueCsvList = await dataSyncArgs.GetValuesCsv(context, command, table, columnsCsvInQuotes).ConfigureAwait(false);
                if (valueCsvList.Count <= 0)
                    continue;

                var arguments = new SyncDataArguments
                {
                    ModuleId = moduleId,
                    Table = table,
                    DbContextTypeName =
                        contextType.AssemblyQualifiedName ??
                        contextType.Name,
                    ColumnsCsv = columnsCsvInQuotes.Replace("\"", string.Empty, StringComparison.Ordinal),
                    ValuesCsv = valueCsvList
                };

                list.Add(arguments);
            }
        }

        return list;
    }

    private sealed class FunctionParameters(Func<IModuleDbContext, DbCommand, Task<List<string>>> getTables,
                                            Func<IModuleDbContext, DbCommand, string, Task<string>> getColumnsCsv,
                                            Func<IModuleDbContext, DbCommand, string, string, Task<List<string>>> getValuesCsv)
    {
        public Func<IModuleDbContext, DbCommand, Task<List<string>>> GetTables { get; } = getTables;
        public Func<IModuleDbContext, DbCommand, string, Task<string>> GetColumnsCsv { get; } = getColumnsCsv;
        public Func<IModuleDbContext, DbCommand, string, string, Task<List<string>>> GetValuesCsv { get; } = getValuesCsv;
    }
}
