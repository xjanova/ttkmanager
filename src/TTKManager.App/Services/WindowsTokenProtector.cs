using System.Runtime.Versioning;
using System.Security.Cryptography;
using System.Text;

namespace TTKManager.App.Services;

[SupportedOSPlatform("windows")]
public sealed class WindowsTokenProtector : ITokenProtector
{
    private static readonly byte[] s_entropy = Encoding.UTF8.GetBytes("TTKManager.v1.token-protect");

    public byte[] Protect(string plaintext)
    {
        var data = Encoding.UTF8.GetBytes(plaintext);
        return ProtectedData.Protect(data, s_entropy, DataProtectionScope.CurrentUser);
    }

    public string Unprotect(byte[] ciphertext)
    {
        var data = ProtectedData.Unprotect(ciphertext, s_entropy, DataProtectionScope.CurrentUser);
        return Encoding.UTF8.GetString(data);
    }
}
