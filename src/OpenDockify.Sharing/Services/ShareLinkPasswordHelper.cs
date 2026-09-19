using System.Security.Cryptography;
using System.Text;

namespace OpenDockify.Sharing.Services;

/// <summary>PBKDF2 link-password hashing with constant-time verify (sharing-owned so the module has no platform refs).</summary>
public static class ShareLinkPasswordHelper
{
    private const int _iterations = 100_000;
    private const int _saltBytes = 16;
    private const int _hashBytes = 32;

    public static (byte[] Hash, byte[] Salt) HashNew(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(_saltBytes);
        return (Compute(password, salt), salt);
    }

    public static byte[] Compute(string password, byte[] salt)
    {
        return Rfc2898DeriveBytes.Pbkdf2(Encoding.UTF8.GetBytes(password), salt, _iterations, HashAlgorithmName.SHA256, _hashBytes);
    }

    public static bool Verify(string password, byte[] hash, byte[] salt)
    {
        return CryptographicOperations.FixedTimeEquals(Compute(password, salt), hash);
    }
}
