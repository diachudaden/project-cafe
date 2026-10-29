using System.Security.Cryptography;
using System.Text;

namespace ChonRieng.Infrastructure.Auth;

public static class RefreshTokenHasher
{
    public static string CreateToken() => Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));

    public static string Hash(string token)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(token));
        return Convert.ToHexString(bytes);
    }
}
