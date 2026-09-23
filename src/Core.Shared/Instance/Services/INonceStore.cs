using Core.Shared.Instance.Contracts;

namespace Core.Shared.Instance.Services;

public interface INonceStore
{
    /// <summary>
    /// Returns the nonce with the given value, or <see langword="null"/> if the store has none.
    /// </summary>
    Task<Nonce?> GetNonce(Guid value, CancellationToken cancellationToken = default);

    Task<Nonce> Create(CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns <see langword="false"/> when the nonce was not in the store.
    /// </summary>
    Task<bool> Delete(Nonce nonce, CancellationToken cancellationToken = default);

    Task DeleteOrphaned(CancellationToken cancellationToken = default);
}
