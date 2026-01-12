using Core.OS.DbContext;
using Core.Shared.Instance.Contracts;
using Core.Shared.Instance.Services;
using Microsoft.EntityFrameworkCore;

namespace Core.OS.Instance.Services;

internal sealed class NonceStore(IApplicationDbContext dbContext) : INonceStore
{
    public async Task<Nonce?> GetNonce(Guid value, CancellationToken cancellationToken = default)
        => await dbContext.Nonces.FindAsync([value], cancellationToken);

    public async Task<Nonce> Create(CancellationToken cancellationToken = default)
    {
        var nonce = new Nonce { Value = Guid.NewGuid(), CreatedAt = DateTimeOffset.UtcNow };

        await dbContext.Nonces.AddAsync(nonce, cancellationToken);
        await dbContext.Instance.SaveChangesAsync(cancellationToken);

        return nonce;
    }

    public async Task<bool> Delete(Nonce nonce, CancellationToken cancellationToken = default)
    {
        dbContext.Instance.Remove(nonce);

        var affectedRows = await dbContext.Instance.SaveChangesAsync(cancellationToken);

        return affectedRows == 1;
    }

    public async Task DeletedOrphaned(CancellationToken cancellationToken = default)
    {
        var expiredAt = DateTimeOffset.UtcNow.AddMinutes(-5);

        var orphanedNonces = await dbContext.Nonces.Where(nonce => nonce.CreatedAt < expiredAt).ToListAsync(cancellationToken);
        if (orphanedNonces.Count > 0)
            dbContext.Nonces.RemoveRange(orphanedNonces);

        await dbContext.Instance.SaveChangesAsync(cancellationToken);
    }
}
