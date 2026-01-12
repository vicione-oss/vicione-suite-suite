using Blazor.Shared.Localization;
using Sdk.Client.Connections;
using Sdk.Connections.Contracts;

namespace Blazor.Shared.Connections.Validators;

public class SQLiteConnectionValidator : ItemValidatorBase<SQLiteConnection>
{
    protected override void ValidateInternal(SQLiteConnection item)
    {
        if (string.IsNullOrWhiteSpace(item.ConnectionString))
        {
            AddError(nameof(SQLiteConnection.ConnectionString), ValidationTerms.ProvideSQLiteConnectionString);
        }
    }
}
