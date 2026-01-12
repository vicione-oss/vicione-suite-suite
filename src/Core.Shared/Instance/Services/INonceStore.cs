using Core.Shared.Instance.Contracts;

namespace Core.Shared.Instance.Services;

/// <summary>
/// Abstraction for a store which manages <see cref="Nonce"/>
/// </summary>
public interface INonceStore
{
    /// <summary>
    /// Gets the nonce with the specified <paramref name="value"/> from the nonce store.
    /// </summary>
    /// <param name="value">Value of the nonce to return</param>
    /// <param name="cancellationToken"><see cref="CancellationToken"/> used to propagate notifications that the operation should be canceled</param>
    /// <returns><see cref="Nonce"/> associated with the given <paramref name="value"/>, otherwise <see langword="null"/></returns>
    Task<Nonce?> GetNonce(Guid value, CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a nonce in the nonce store.
    /// </summary>
    /// <param name="cancellationToken"><see cref="CancellationToken"/> used to propagate notifications that the operation should be canceled</param>
    /// <returns><see cref="Task"/> that represents the asynchronous operation, containing the <see cref="Nonce"/> of the creation operation</returns>
    Task<Nonce> Create(CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes the specified <paramref name="nonce"/> from the nonce store.
    /// </summary>
    /// <param name="nonce">Nonce to delete</param>
    /// <param name="cancellationToken"><see cref="CancellationToken"/> used to propagate notifications that the operation should be canceled</param>
    /// <returns><see langword="true"/> when the specified <paramref name="nonce"/> was deleted, otherwise <see langword="false"/></returns>
    Task<bool> Delete(Nonce nonce, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes orphaned nonces from the nonce store.
    /// </summary>
    /// <param name="cancellationToken"><see cref="CancellationToken"/> used to propagate notifications that the operation should be canceled</param>
    Task DeletedOrphaned(CancellationToken cancellationToken = default);
}
