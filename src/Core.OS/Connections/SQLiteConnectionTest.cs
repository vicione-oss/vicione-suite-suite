using System.IO.Abstractions;
using Core.OS.Instance;
using Core.OS.Instance.Extensions;
using Core.Shared;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Sdk.Connections.Contracts;

namespace Core.OS.Connections;

public sealed class SQLiteConnectionTest(IOptions<InstanceOptions> instanceOptions) : IConnectionTest
{
    public async Task<ConnectionTestResult> Test(IConnection connection, CancellationToken cancellationToken)
    {
        if (connection is not SQLiteConnection sqliteConnection)
            return ConnectionTestResultFactory.CreateFailureResult("Invalid connection type for Postgres test");

        try
        {
            ArgumentNullException.ThrowIfNull(sqliteConnection);

            var optionsBuilder = new DbContextOptionsBuilder();

            ValidateConnectionStringContainsParameters(sqliteConnection);
            RootSqliteDataSourcePath(sqliteConnection, instanceOptions.Value);
            optionsBuilder.UseSqlite(sqliteConnection.ConnectionString);

            await using var context = new Microsoft.EntityFrameworkCore.DbContext(optionsBuilder.Options);

            if (!await context.Database.CanConnectAsync(cancellationToken))
                return ConnectionTestResultFactory.CreateFailureResult($"Can't connect to SQLite database");
        }
        catch (Exception ex)
        {
            return ConnectionTestResultFactory.CreateFailureResult($"Database connection test failed: {ex.Message}");
        }

        return new(true, null);
    }

    private static void RootSqliteDataSourcePath(SQLiteConnection spliteConnection, InstanceOptions instanceOptions)
    {
        if (string.IsNullOrEmpty(instanceOptions.HomeDirectory))
            throw new ConfigurationException(nameof(InstanceOptions.HomeDirectory));

        // https://learn.microsoft.com/en-us/dotnet/standard/data/sqlite/connection-strings
        var optionsBuilder = new SqliteConnectionStringBuilder(spliteConnection.ConnectionString);

        if (string.IsNullOrEmpty(optionsBuilder.DataSource))
            throw new InvalidOperationException($"{nameof(optionsBuilder.DataSource)} is empty");

        // check for relative path
        if (Path.IsPathFullyQualified(optionsBuilder.DataSource) || optionsBuilder.DataSource.Contains(":memory:", StringComparison.InvariantCultureIgnoreCase))
            return;

        var fs = new FileSystem();
        optionsBuilder.DataSource = fs.Path.Combine(fs.GetRootedHomeDirectory(instanceOptions), optionsBuilder.DataSource);
        spliteConnection.ConnectionString = optionsBuilder.ConnectionString;
    }

    private static void ValidateConnectionStringContainsParameters(SQLiteConnection sqliteConnection)
    {
        if (!sqliteConnection.ConnectionString.Contains('=', StringComparison.Ordinal))
            throw new InvalidOperationException($"{nameof(sqliteConnection.ConnectionString)} has no parameters");
    }
}
