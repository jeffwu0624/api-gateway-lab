using TokenService.Domain.Entities;

namespace TokenService.Domain.Interfaces;

public interface IUserRepository
{
    Task<AppUser?> FindByUsernameAsync(string username, CancellationToken ct = default);
    Task<AppUser?> FindByIdAsync(int id, CancellationToken ct = default);
}
