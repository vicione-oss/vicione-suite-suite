using System.IO.Abstractions;
using Core.OS.Instance;
using Core.OS.Instance.Extensions;
using Core.Shared;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Sdk.Connections.Contracts;

namespace Core.OS.Connections;

public sealed class DatabaseConnectionTest(IOptions<InstanceOptions> instanceOptions) : IConnectionTest
{
    public async Task<ConnectionTestResult> Test(IConnection connection, CancellationToken cancellationToken)
    {
        if (connection is not DatabaseConnection dbConnection)
            return ConnectionTestResultFactory.CreateFailureResult("Invalid connection type for Postgres test");

        try
        {
            ArgumentNullException.ThrowIfNull(dbConnection);

            var optionsBuilder = new DbContextOptionsBuilder();
            switch (dbConnection.DatabaseType)
            {
                case DatabaseConnectionType.Postgres:
                    ValidateConnectionStringContainsParameters(dbConnection);
                    optionsBuilder.UseNpgsql(dbConnection.ConnectionString);
                    break;

                case DatabaseConnectionType.SQLite:
                    ValidateConnectionStringContainsParameters(dbConnection);
                    RootSqliteDataSourcePath(dbConnection, instanceOptions.Value);
                    optionsBuilder.UseSqlite(dbConnection.ConnectionString);
                    break;

                default: throw new ArgumentException($"Testing {dbConnection.DatabaseType} connection is not supported yet");
            }

            await using var context = new Microsoft.EntityFrameworkCore.DbContext(optionsBuilder.Options);

            if (!await context.Database.CanConnectAsync(cancellationToken))
                return ConnectionTestResultFactory.CreateFailureResult($"Can't connect to {dbConnection.DatabaseType} database");
        }
        catch (Exception ex)
        {
            return ConnectionTestResultFactory.CreateFailureResult($"Database connection test failed: {ex.Message}");
        }

        return new(true, null);
    }

    private static void RootSqliteDataSourcePath(DatabaseConnection dbConnection, InstanceOptions instanceOptions)
    {
        if (dbConnection.DatabaseType != DatabaseConnectionType.SQLite)
            return;

        if (string.IsNullOrEmpty(instanceOptions.HomeDirectory))
            throw new ConfigurationException(nameof(InstanceOptions.HomeDirectory));

        // https://learn.microsoft.com/en-us/dotnet/standard/data/sqlite/connection-strings
        var optionsBuilder = new SqliteConnectionStringBuilder(dbConnection.ConnectionString);

        if (string.IsNullOrEmpty(optionsBuilder.DataSource))
            throw new InvalidOperationException($"{nameof(optionsBuilder.DataSource)} is empty");

        // check for relative path
        if (Path.IsPathFullyQualified(optionsBuilder.DataSource) || optionsBuilder.DataSource.Contains(":memory:", StringComparison.InvariantCultureIgnoreCase))
            return;

        var fs = new FileSystem();
        optionsBuilder.DataSource = fs.Path.Combine(fs.GetRootedHomeDirectory(instanceOptions), optionsBuilder.DataSource);
        dbConnection.ConnectionString = optionsBuilder.ConnectionString;
    }

    private static void ValidateConnectionStringContainsParameters(DatabaseConnection dbConnection)
    {
        if (!dbConnection.ConnectionString.Contains('=', StringComparison.Ordinal))
            throw new InvalidOperationException($"{nameof(dbConnection.ConnectionString)} has no parameters");
    }
}
