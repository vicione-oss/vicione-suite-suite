namespace Core.OS.Hosting.Contracts;

internal record PreparationSuccessResult : IPreparationSuccessResult;

internal record VersionDowngradePreparationResult(VersionDowngradeInformation DowngradeInformation) : IPreparationAbortResult
{
    public string Reason => $"Downgrade detected from version '{DowngradeInformation.DataVersion}' to '{DowngradeInformation.CurrentVersion}'";
}
