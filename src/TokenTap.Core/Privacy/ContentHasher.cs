using System.Security.Cryptography;
using System.Text;

namespace TokenTap.Core.Privacy;

public static class ContentHasher
{
    public static string Sha256Hex(string value)
    {
        byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    public static string Sha256FilePath(string path) =>
        Sha256Hex(Path.GetFullPath(path).ToUpperInvariant());
}
