using System.Security.Cryptography;
using System.Text;

namespace FunApp.Api.Auth;

public static class SessionToken
{
    public static string Create()
    {
        return Base64UrlEncode(RandomNumberGenerator.GetBytes(32));
    }

    public static string Hash(string token)
    {
        return Base64UrlEncode(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
    }

    private static string Base64UrlEncode(byte[] bytes)
    {
        return Convert.ToBase64String(bytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }
}
