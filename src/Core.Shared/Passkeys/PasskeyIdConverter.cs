using System.Buffers.Text;

namespace Core.Shared.Passkeys;

public static class PasskeyIdConverter
{
    public static string EncodePasskeyId(byte[] passkeyId) =>
        Base64Url.EncodeToString(passkeyId);

    public static byte[] DecodePasskeyId(string passkeyId) =>
        Base64Url.DecodeFromChars(passkeyId);
}
