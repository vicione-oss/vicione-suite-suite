namespace Blazor.Shared.Settings.Models;

public interface IUploadTicket
{
    CancellationToken CancellationToken { get; }

    void Cancel();
}
