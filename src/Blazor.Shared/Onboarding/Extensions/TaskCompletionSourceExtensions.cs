using Sdk.Client.Wizards.Models;
using Sdk.Messaging;
using ViciOne.Ui.Localization.Resources;

namespace Blazor.Shared.Onboarding.Extensions;

public static class TaskCompletionSourceExtensions
{
    extension(TaskCompletionSource<ErrorInfo?> taskCompletionSource)
    {
        public Task<ISaveResult> WaitForCommandCompletion(CancellationToken cancellationToken = default)
            => taskCompletionSource.WaitForCommandCompletion(errorInfo => new SaveErrorResult(errorInfo.Message ?? CommonPhrases.AnUnexpectedErrorOccurred, errorInfo.ErrorCode), cancellationToken);

        public async Task<ISaveResult> WaitForCommandCompletion(Func<ErrorInfo, ISaveResult> errorOccured,
            CancellationToken cancellationToken = default)
        {
            try
            {
                var errorInfo = await taskCompletionSource.Task.WaitAsync(TimeSpan.FromMilliseconds(Shared.Constants.CommandTimeoutMs),
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
}
