namespace TokenService.Application.Interfaces;

public interface IRefreshTokenGenerator
{
    (string rawToken, string tokenHash) Generate();
}
