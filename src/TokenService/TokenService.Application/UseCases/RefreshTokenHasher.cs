using System.Security.Cryptography;
using System.Text;

namespace TokenService.Application.UseCases;

public static class RefreshTokenHasher
{
    public static string ComputeHex(string rawToken)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(rawToken));
        return Convert.ToHexString(hash);
    }
}
