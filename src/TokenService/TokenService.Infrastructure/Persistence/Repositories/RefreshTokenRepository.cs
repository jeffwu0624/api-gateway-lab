using Microsoft.EntityFrameworkCore;
using TokenService.Application.Exceptions;
using TokenService.Domain.Entities;
using TokenService.Domain.Interfaces;

namespace TokenService.Infrastructure.Persistence.Repositories;

public class RefreshTokenRepository(AppDbContext db) : IRefreshTokenRepository
{
    public Task<RefreshToken?> FindByHashAsync(string tokenHash, CancellationToken ct = default)
        => db.RefreshTokens.FirstOrDefaultAsync(r => r.TokenHash == tokenHash, ct);

    public async Task AddAsync(RefreshToken token, CancellationToken ct = default)
        => await db.RefreshTokens.AddAsync(token, ct);

    public async Task RevokeAllForUserAsync(int userId, CancellationToken ct = default)
    {
        var tokens = await db.RefreshTokens
            .Where(r => r.UserId == userId && !r.IsRevoked)
            .ToListAsync(ct);
        tokens.ForEach(t => t.Revoke());
    }

    public async Task SaveChangesAsync(CancellationToken ct = default)
    {
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateConcurrencyException ex)
        { throw new ConcurrencyException("RT 已被並發修改，請重試。", ex); }
    }
}
