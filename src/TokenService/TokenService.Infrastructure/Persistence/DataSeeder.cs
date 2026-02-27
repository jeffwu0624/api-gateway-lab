using Microsoft.EntityFrameworkCore;
using TokenService.Domain.Entities;

namespace TokenService.Infrastructure.Persistence;

public static class DataSeeder
{
    public static async Task SeedAsync(AppDbContext db)
    {
        if (await db.Users.AnyAsync()) return;

        db.Users.AddRange(
            AppUser.CreateWindowsUser("jeff.wang", ["admin", "orders.read", "orders.write"]),
            AppUser.CreateWindowsUser("alice.chen", ["viewer", "orders.read"])
        );
        await db.SaveChangesAsync();
    }
}
