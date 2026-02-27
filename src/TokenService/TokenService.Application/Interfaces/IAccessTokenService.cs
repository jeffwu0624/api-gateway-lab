using TokenService.Domain.Entities;

namespace TokenService.Application.Interfaces;

public interface IAccessTokenService
{
    string Generate(AppUser user);
}
