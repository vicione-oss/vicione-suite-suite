namespace Core.OS.Hosting.Contracts;

internal record PreparationSuccessResult : IPreparationSuccessResult;

internal record VersionDowngradePreparationResult(VersionDowngradeInformation DowngradeInformation) : IPreparationAbortResult
{
    public string Reason => $"Downgrade detected from version '{DowngradeInformation.DataVersion}' to '{DowngradeInformation.CurrentVersion}'";
}

internal record RecoveryExhaustedPreparationResult : IPreparationAbortResult
{
    public string Reason => "Recovery mode was already activated, but the suite continues to crash. No further self-healing possible — manual intervention required.";
}

internal record ModuleHostPreparationResult(string Reason) : IPreparationAbortResult;
