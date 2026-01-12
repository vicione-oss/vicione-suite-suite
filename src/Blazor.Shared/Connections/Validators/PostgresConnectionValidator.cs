using Blazor.Shared.Localization;
using Sdk.Client.Connections;
using Sdk.Connections.Contracts;

namespace Blazor.Shared.Connections.Validators;

public class PostgresConnectionValidator : ItemValidatorBase<PostgresConnection>
{
    protected override void ValidateInternal(PostgresConnection item)
    {
        if (string.IsNullOrWhiteSpace(item.ConnectionString))
        {
            AddError(nameof(PostgresConnection.ConnectionString), ValidationTerms.ProvidePostgresConnectionString);
        }
    }
}
