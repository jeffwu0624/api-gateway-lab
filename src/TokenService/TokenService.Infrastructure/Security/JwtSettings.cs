namespace TokenService.Infrastructure.Security;

public record JwtSettings(
    string Issuer,
    string Audience,
    string PrivateKeyPath,
    string PublicKeyPath,
    int AccessTokenMinutes = 60);
