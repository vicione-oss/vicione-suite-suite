using Blazor.Shared.Wizard.Models;
using Sdk.Messaging;
using ViciOne.Ui.Localization.Resources;

namespace Blazor.Shared.Onboarding.Extensions;

public static class TaskCompletionSourceExtensions
{
    public static Task<ISaveResult> WaitForCommandCompletion(this TaskCompletionSource<ErrorInfo?> taskCompletionSource, CancellationToken cancellationToken = default)
        => taskCompletionSource.WaitForCommandCompletion(errorInfo => new SaveErrorResult(errorInfo.Message ?? CommonPhrases.AnUnexpectedErrorOccurred, errorInfo.ErrorCode), cancellationToken);

    public static async Task<ISaveResult> WaitForCommandCompletion(this TaskCompletionSource<ErrorInfo?> taskCompletionSource,
        Func<ErrorInfo, ISaveResult> errorOccured,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var errorInfo = await taskCompletionSource.Task.WaitAsync(TimeSpan.FromMilliseconds(Blazor.Shared.Constants.CommandTimeoutMs),
                cancellationToken);

            if (taskCompletionSource.Task.IsCanceled)
                return new SaveSuccessResult();

            if (errorInfo is not null)
                return errorOccured.Invoke(errorInfo);

            return new SaveSuccessResult();
        }
        catch (TimeoutException)
        {
            return new SaveErrorResult(CommonPhrases.TheOperationHasTimedOut);
        }
    }
}
