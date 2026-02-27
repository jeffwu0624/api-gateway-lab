using FluentAssertions;
using Moq;
using TokenService.Application.Exceptions;
using TokenService.Application.Interfaces;
using TokenService.Application.UseCases;
using TokenService.Domain.Entities;
using TokenService.Domain.Interfaces;

namespace TokenService.Application.Tests;

public class RefreshTokenUseCaseTests
{
    private readonly Mock<IRefreshTokenRepository> _refreshTokens = new();
    private readonly Mock<IUserRepository> _users = new();
    private readonly Mock<IAccessTokenService> _tokenService = new();
    private readonly Mock<IRefreshTokenGenerator> _rtGenerator = new();

    private RefreshTokenUseCase Sut() => new(
        _refreshTokens.Object, _users.Object, _tokenService.Object, _rtGenerator.Object);

    [Fact]
    public async Task Execute_ValidToken_ReturnsNewTokenPair()
    {
        var rawRt = "valid-raw-token";
        var hash = RefreshTokenHasher.ComputeHex(rawRt);
        var rt = RefreshToken.Create(1, hash, DateTime.UtcNow.AddDays(30));
        var user = AppUser.CreateWindowsUser("jeff.wang", ["orders.read"]);

        _refreshTokens.Setup(r => r.FindByHashAsync(hash, default)).ReturnsAsync(rt);
        _users.Setup(r => r.FindByIdAsync(1, default)).ReturnsAsync(user);
        _tokenService.Setup(s => s.Generate(user)).Returns("new-at");
        _rtGenerator.Setup(g => g.Generate()).Returns(("new-raw-rt", "new-hash"));

        var result = await Sut().ExecuteAsync(rawRt);

        result.AccessToken.Should().Be("new-at");
        result.RefreshToken.Should().Be("new-raw-rt");
    }

    [Fact]
    public async Task Execute_InvalidHash_ThrowsUnauthorized()
    {
        _refreshTokens.Setup(r => r.FindByHashAsync(It.IsAny<string>(), default))
                      .ReturnsAsync((RefreshToken?)null);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => Sut().ExecuteAsync("invalid-token"));
    }

    [Fact]
    public async Task Execute_RevokedPastGrace_ThrowsTokenReuse()
    {
        var rawRt = "reused-token";
        var hash = RefreshTokenHasher.ComputeHex(rawRt);
        var rt = RefreshToken.Create(1, hash, DateTime.UtcNow.AddDays(30));
        rt.Revoke();

        // Simulate RevokedAt being more than 60 seconds ago
        var revokedAtProp = typeof(RefreshToken).GetProperty("RevokedAt")!;
        revokedAtProp.SetValue(rt, DateTime.UtcNow.AddSeconds(-120));

        _refreshTokens.Setup(r => r.FindByHashAsync(hash, default)).ReturnsAsync(rt);

        await Assert.ThrowsAsync<TokenReuseException>(
            () => Sut().ExecuteAsync(rawRt));
    }

    [Fact]
    public async Task Execute_ExpiredToken_ThrowsUnauthorized()
    {
        var rawRt = "expired-token";
        var hash = RefreshTokenHasher.ComputeHex(rawRt);
        var rt = RefreshToken.Create(1, hash, DateTime.UtcNow.AddDays(-1));

        _refreshTokens.Setup(r => r.FindByHashAsync(hash, default)).ReturnsAsync(rt);

        var ex = await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => Sut().ExecuteAsync(rawRt));
        ex.Message.Should().Be("token_expired");
    }
}
