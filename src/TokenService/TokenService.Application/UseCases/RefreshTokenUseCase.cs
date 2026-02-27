using TokenService.Application.DTOs;
using TokenService.Application.Exceptions;
using TokenService.Application.Interfaces;
using TokenService.Domain.Entities;
using TokenService.Domain.Interfaces;

namespace TokenService.Application.UseCases;

public class RefreshTokenUseCase(
    IRefreshTokenRepository refreshTokens,
    IUserRepository users,
    IAccessTokenService tokenService,
    IRefreshTokenGenerator rtGenerator)
{
    private const int GracePeriodSeconds = 60;

    public async Task<TokenResponse> ExecuteAsync(string rawRefreshToken, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(rawRefreshToken))
            throw new UnauthorizedAccessException("invalid_token");

        var hash = RefreshTokenHasher.ComputeHex(rawRefreshToken);
        var rt = await refreshTokens.FindByHashAsync(hash, ct)
            ?? throw new UnauthorizedAccessException("invalid_token");

        if (rt.IsRevoked)
        {
            var graceCutoff = rt.RevokedAt?.AddSeconds(GracePeriodSeconds) ?? DateTime.MinValue;
            if (DateTime.UtcNow > graceCutoff)
            {
                await refreshTokens.RevokeAllForUserAsync(rt.UserId, ct);
                await refreshTokens.SaveChangesAsync(ct);
                throw new TokenReuseException("token_reuse_detected");
            }
        }

        if (rt.IsExpired)
            throw new UnauthorizedAccessException("token_expired");

        rt.Revoke();
        await refreshTokens.SaveChangesAsync(ct);

        var user = await users.FindByIdAsync(rt.UserId, ct)
            ?? throw new UnauthorizedAccessException("user_not_found");

        var newAt = tokenService.Generate(user);
        var (rawRt2, hash2) = rtGenerator.Generate();
        await refreshTokens.AddAsync(
            RefreshToken.Create(rt.UserId, hash2, DateTime.UtcNow.AddDays(30)), ct);
        await refreshTokens.SaveChangesAsync(ct);

        return new TokenResponse(newAt, rawRt2, "Bearer", 3600, "");
    }
}
