namespace Core.Shared.Security;

public interface IAccountVerification
{
    Task<bool> NeedsVerification(string userId, CancellationToken cancellationToken = default);
}
