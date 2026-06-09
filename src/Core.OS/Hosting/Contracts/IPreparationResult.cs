namespace Core.OS.Hosting.Contracts;

internal interface IPreparationResult;

internal interface IPreparationSuccessResult : IPreparationResult;

internal interface IPreparationAbortResult : IPreparationResult
{
    string Reason { get; }
}
