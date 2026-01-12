using Microsoft.EntityFrameworkCore;
using Sdk.Connections.Contracts;

namespace Core.OS.Connections;

public sealed class PostgresConnectionTest : IConnectionTest
{
    public async Task<ConnectionTestResult> Test(IConnection connection, CancellationToken cancellationToken)
    {
        if (connection is not PostgresConnection postgresConnection)
            return ConnectionTestResultFactory.CreateFailureResult("Invalid connection type for Postgres test");

        try
        {
            ArgumentNullException.ThrowIfNull(postgresConnection);

            var optionsBuilder = new DbContextOptionsBuilder();

            ValidateConnectionStringContainsParameters(postgresConnection);
            optionsBuilder.UseNpgsql(postgresConnection.ConnectionString);

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

    private static void ValidateConnectionStringContainsParameters(PostgresConnection postgresConnection)
    {
        if (!postgresConnection.ConnectionString.Contains('=', StringComparison.Ordinal))
            throw new InvalidOperationException($"{nameof(postgresConnection.ConnectionString)} has no parameters");
    }
}
