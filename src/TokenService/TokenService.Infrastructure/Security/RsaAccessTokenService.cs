using System.Security.Claims;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using TokenService.Application.Interfaces;
using TokenService.Domain.Entities;

namespace TokenService.Infrastructure.Security;

public class RsaAccessTokenService(JwtSettings settings) : IAccessTokenService
{
    public string Generate(AppUser user)
    {
        using var rsa = RSA.Create();
        rsa.ImportFromPem(File.ReadAllText(settings.PrivateKeyPath));

        return new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity([
                new Claim(JwtRegisteredClaimNames.Sub, user.Username),
                new Claim(JwtRegisteredClaimNames.Name, user.Username),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
                new Claim("roles",
                          JsonSerializer.Serialize(user.Roles),
                          JsonClaimValueTypes.JsonArray),
                new Claim("scope",
                          string.Join(" ", user.Roles.Where(r => r.Contains('.'))))
            ]),
            Issuer = settings.Issuer,
            Audience = settings.Audience,
            IssuedAt = DateTime.UtcNow,
            Expires = DateTime.UtcNow.AddMinutes(settings.AccessTokenMinutes),
            SigningCredentials = new SigningCredentials(
                new RsaSecurityKey(rsa), SecurityAlgorithms.RsaSha256)
        });
    }
}
