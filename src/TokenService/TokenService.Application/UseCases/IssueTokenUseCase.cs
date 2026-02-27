using TokenService.Application.DTOs;
using TokenService.Application.Interfaces;
using TokenService.Domain.Entities;
using TokenService.Domain.Interfaces;

namespace TokenService.Application.UseCases;

public class IssueTokenUseCase(
    IUserRepository users,
    IRefreshTokenRepository refreshTokens,
    IAccessTokenService tokenService,
    IRefreshTokenGenerator rtGenerator)
{
    public async Task<TokenResponse> ExecuteAsync(string windowsUsername, CancellationToken ct = default)
    {
        var username = NormalizeWindowsUsername(windowsUsername);

        var user = await users.FindByUsernameAsync(username, ct)
            ?? throw new UnauthorizedAccessException($"User '{username}' not found.");

        var at = tokenService.Generate(user);
        var (rawRt, rtHash) = rtGenerator.Generate();
        var rt = RefreshToken.Create(user.Id, rtHash, DateTime.UtcNow.AddDays(30));

        await refreshTokens.AddAsync(rt, ct);
        await refreshTokens.SaveChangesAsync(ct);

        var scope = string.Join(" ", user.Roles.Where(r => r.Contains('.')));
        return new TokenResponse(at, rawRt, "Bearer", 3600, scope);
    }

    private static string NormalizeWindowsUsername(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            throw new UnauthorizedAccessException("invalid_windows_username");

        var normalized = raw.Trim();
        var separatorIndex = normalized.IndexOf('\\');
        if (separatorIndex >= 0)
            normalized = normalized[(separatorIndex + 1)..].Trim();

        if (string.IsNullOrWhiteSpace(normalized))
            throw new UnauthorizedAccessException("invalid_windows_username");

        return normalized.ToLowerInvariant();
    }
}
