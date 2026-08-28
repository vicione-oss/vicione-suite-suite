using Core.Tests.Tools;
using Npgsql;

namespace Core.OS.Tests;

/// <summary>
/// One place for the PostgreSQL server the database tests connect to, resolved from the environment so the same
/// tests reach a local server and the service container CI provides. The defaults match the credentials the rest
/// of the repository uses for PostgreSQL — launch profiles, compose, and the CI jobs — so a developer running a
/// stock local server needs no environment setup. See docs/integration-testing.md.
/// </summary>
public static class PostgresTestConnection
{
    public static string ForDatabase(string database)
        => new NpgsqlConnectionStringBuilder
        {
            Host = IntegrationServiceSettings.GetHost("POSTGRES_HOST", "127.0.0.1"),
            Port = IntegrationServiceSettings.GetPort("POSTGRES_PORT", 5432),
            Database = database,
            Username = IntegrationServiceSettings.GetValue("POSTGRES_USER", "postgres"),
            Password = IntegrationServiceSettings.GetValue("POSTGRES_PASSWORD", "postgres"),
            IncludeErrorDetail = true
        }.ConnectionString;
}
