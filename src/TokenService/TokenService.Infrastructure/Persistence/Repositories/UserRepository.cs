using Microsoft.EntityFrameworkCore;
using TokenService.Domain.Entities;
using TokenService.Domain.Interfaces;

namespace TokenService.Infrastructure.Persistence.Repositories;

public class UserRepository(AppDbContext db) : IUserRepository
{
    public Task<AppUser?> FindByUsernameAsync(string username, CancellationToken ct = default)
        => db.Users.FirstOrDefaultAsync(u => u.Username == username && u.IsActive, ct);

    public Task<AppUser?> FindByIdAsync(int id, CancellationToken ct = default)
        => db.Users.FirstOrDefaultAsync(u => u.Id == id && u.IsActive, ct);
}
