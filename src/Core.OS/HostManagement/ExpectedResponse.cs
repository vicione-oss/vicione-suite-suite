namespace Core.OS.HostManagement;

internal class ExpectedResponse
{
    public TaskCompletionSource<string> TaskCompletionSource { get; } =
        new(TaskCreationOptions.RunContinuationsAsynchronously);
}
