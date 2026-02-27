using System.Security.Cryptography;
using TokenService.Application.Interfaces;
using TokenService.Application.UseCases;

namespace TokenService.Infrastructure.Security;

public class CryptoRefreshTokenGenerator : IRefreshTokenGenerator
{
    public (string rawToken, string tokenHash) Generate()
    {
        var raw = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));
        return (raw, RefreshTokenHasher.ComputeHex(raw));
    }
}
