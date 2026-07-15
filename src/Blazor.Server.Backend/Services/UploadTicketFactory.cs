
using Sdk.Client.Models;
using Sdk.Client.Services;

namespace Blazor.Server.Backend.Services;

public class UploadTicketFactory : IUploadTicketFactory
{
    public IUploadTicket CreateUploadTicket() => new UploadTicket();

    private class UploadTicket : IUploadTicket, IDisposable
    {
        private readonly CancellationTokenSource _cancellationTokenSource = new();

        private bool _disposed;

        public CancellationToken CancellationToken => _cancellationTokenSource.Token;

        public void Dispose()
        {
            if (_disposed)
                return;

            _cancellationTokenSource.Cancel();
            _cancellationTokenSource.Dispose();

            _disposed = true;
        }

        public void Cancel()
        {
            try
            {
                _cancellationTokenSource.Cancel();
            }
            catch (ObjectDisposedException)
            {
                // CancellationTokenSource already disposed, return gracefully
            }
        }
    }
}
