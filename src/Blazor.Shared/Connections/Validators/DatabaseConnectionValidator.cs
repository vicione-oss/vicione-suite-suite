using Blazor.Shared.Localization;
using Sdk.Client.Connections;
using Sdk.Connections.Contracts;

namespace Blazor.Shared.Connections.Validators;

public class DatabaseConnectionValidator : ItemValidatorBase<DatabaseConnection>
{
    protected override void ValidateInternal(DatabaseConnection item)
    {
        if (string.IsNullOrWhiteSpace(item.ConnectionString))
        {
            AddError(nameof(DatabaseConnection.ConnectionString), ValidationTerms.ProvideDatabaseConnectionString);
        }
    }
}
