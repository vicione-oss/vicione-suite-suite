using System.Net;

namespace Core.Shared.Passkeys;

public sealed class PasskeyHostSupport : IPasskeyHostSupport
{
    public bool IsPasskeyCapableHost(string? host)
        => !string.IsNullOrWhiteSpace(host) && !IsIpLiteral(host);

    private static bool IsIpLiteral(string host) => IPAddress.TryParse(host, out _);
}
