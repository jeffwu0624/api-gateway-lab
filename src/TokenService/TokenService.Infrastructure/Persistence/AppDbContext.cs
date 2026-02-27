using Microsoft.EntityFrameworkCore;
using TokenService.Domain.Entities;
using TokenService.Infrastructure.Persistence.Configurations;

namespace TokenService.Infrastructure.Persistence;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<AppUser> Users => Set<AppUser>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    protected override void OnModelCreating(ModelBuilder mb)
    {
        mb.ApplyConfiguration(new AppUserConfiguration());
        mb.ApplyConfiguration(new RefreshTokenConfiguration());
    }

    public override Task<int> SaveChangesAsync(CancellationToken ct = default)
    {
        foreach (var entry in ChangeTracker.Entries<RefreshToken>()
                     .Where(e => e.State == EntityState.Modified))
        {
            entry.Property("RowVersion").CurrentValue = Guid.NewGuid();
        }
        return base.SaveChangesAsync(ct);
    }
}
