using System.Data.Common;
using System.Globalization;
using System.Text;

namespace Core.OS.DbContext.Extensions;

public static class DbCommandExtensions
{
    extension(DbCommand command)
    {
        public async Task<List<string>> GetTablesPg(string schemaName, CancellationToken cancellationToken)
        {
            var sql = $"SELECT tablename FROM pg_catalog.pg_tables WHERE schemaname = '{schemaName}';";
            return await command.GetTables(sql, cancellationToken);
        }

        public async Task<List<string>> GetTablesSqlite(CancellationToken cancellationToken)
        {
            const string sql = "SELECT tbl_name FROM sqlite_schema where name not like \"sqlite%\" and name not like \"%EFMigration%\";";
            return await command.GetTables(sql, cancellationToken);
        }

        private async Task<List<string>> GetTables(string sql, CancellationToken cancellationToken)
        {
            var tableList = new List<string>();
            command.CommandText = sql;
            await using var tableReader = await command.ExecuteReaderAsync(cancellationToken);
            while (await tableReader.ReadAsync(cancellationToken))
            {
                if (tableReader.HasRows)
                    tableList.Add(tableReader.GetString(0));
            }
            return tableList;
        }

        public async Task<string> GetColumnsCsvPg(string schema, string table, CancellationToken cancellationToken)
        {
            var sql = $"SELECT column_name FROM information_schema.columns WHERE table_schema = '{schema}' AND table_name = '{table}'";
            return await command.GetColumnsCsv(sql, 0, cancellationToken);
        }

        public async Task<string> GetColumnsCsvSqlite(string table, CancellationToken cancellationToken)
        {
            var sql = $"pragma table_info('{table}');";
            return await command.GetColumnsCsv(sql, 1, cancellationToken);
        }

        private async Task<string> GetColumnsCsv(string sql, int column, CancellationToken cancellationToken)
        {
            var sb = new StringBuilder();
            command.CommandText = sql;
            await using var columnReader = await command.ExecuteReaderAsync(cancellationToken);
            var readOn = await columnReader.ReadAsync(cancellationToken);
            while (readOn)
            {
                if (columnReader.HasRows)
                {
                    sb.Append('"');
                    sb.Append(columnReader.GetString(column));
                    sb.Append('"');
                }
                readOn = await columnReader.ReadAsync(cancellationToken);
                if (readOn)
                    sb.Append(',');
            }
            return sb.ToString();
        }

        public async Task<List<string>> GetValuesCsvPg(string schema, string table, string columnsCsv, CancellationToken cancellationToken)
        {
            var sql = $"SELECT {columnsCsv} FROM \"{schema}\".\"{table}\"";
            return await command.GetValuesCsv(sql, cancellationToken);
        }

        public async Task<List<string>> GetValuesCsvSqlite(string table, string columnsCsv, CancellationToken cancellationToken)
        {
            var sql = $"SELECT {columnsCsv} FROM \"{table}\"";
            return await command.GetValuesCsv(sql, cancellationToken);
        }

        private async Task<List<string>> GetValuesCsv(string sql, CancellationToken cancellationToken)
        {
            var sb = new StringBuilder();
            var valueCsvList = new List<string>();
            command.CommandText = sql;
            await using var valueReader = await command.ExecuteReaderAsync(cancellationToken);
            while (await valueReader.ReadAsync(cancellationToken))
            {
                if (!valueReader.HasRows)
                    continue;

                for (var i = 0; i < valueReader.FieldCount; i++)
                {
                    var dataTypeName = valueReader.GetDataTypeName(i);
                    if (await valueReader.IsDBNullAsync(i, cancellationToken))
                    {
                        sb.Append("null");
                    }
                    else if (dataTypeName.Equals("text", StringComparison.OrdinalIgnoreCase)
                             || dataTypeName.StartsWith("character", StringComparison.OrdinalIgnoreCase))
                    {
                        var value = valueReader.GetString(i);
                        if (value.Contains('\'', StringComparison.InvariantCultureIgnoreCase))
                            value = value.Replace("'", "''", StringComparison.InvariantCultureIgnoreCase);
                        sb.Append('\'');
                        sb.Append(value);
                        sb.Append('\'');
                    }
                    else if (dataTypeName.Equals("uuid", StringComparison.OrdinalIgnoreCase))
                    {
                        sb.Append('\'');
                        // if guids are lowercase inserted into SQLite comparison on guids fail!
                        sb.Append(valueReader.GetGuid(i).ToString().ToUpperInvariant());
                        sb.Append('\'');
                    }
                    else if (dataTypeName.Equals("date", StringComparison.OrdinalIgnoreCase))
                    {
                        sb.Append('\'');
                        sb.Append(valueReader.GetDateTime(i).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
                        sb.Append('\'');
                    }
                    else if (dataTypeName.Equals("time", StringComparison.OrdinalIgnoreCase))
                    {
                        sb.Append('\'');
                        sb.Append(valueReader.GetDateTime(i).ToString("HH:mm:ss.fff", CultureInfo.InvariantCulture));
                        sb.Append('\'');
                    }
                    else if (dataTypeName.StartsWith("date", StringComparison.OrdinalIgnoreCase)
                             || dataTypeName.StartsWith("time", StringComparison.OrdinalIgnoreCase))
                    {
                        sb.Append('\'');
                        sb.Append(valueReader.GetDateTime(i).ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture));
                        sb.Append('\'');
                    }
                    else
                    {
                        sb.Append(valueReader.GetValue(i));
                    }
                    if (i != valueReader.FieldCount - 1)
                        sb.Append(',');
                }

                valueCsvList.Add(sb.ToString());
                sb.Clear();
            }
            return valueCsvList;
        }
    }
}
