namespace Core.Shared.Passkeys;

public interface IPasskeyHostSupport
{
    bool IsPasskeyCapableHost(string? host);
}
