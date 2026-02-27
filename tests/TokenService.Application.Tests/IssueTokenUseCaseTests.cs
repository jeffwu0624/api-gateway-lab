using FluentAssertions;
using Moq;
using TokenService.Application.Interfaces;
using TokenService.Application.UseCases;
using TokenService.Domain.Entities;
using TokenService.Domain.Interfaces;

namespace TokenService.Application.Tests;

public class IssueTokenUseCaseTests
{
    private readonly Mock<IUserRepository> _users = new();
    private readonly Mock<IRefreshTokenRepository> _refreshTokens = new();
    private readonly Mock<IAccessTokenService> _tokenService = new();
    private readonly Mock<IRefreshTokenGenerator> _rtGenerator = new();

    private IssueTokenUseCase Sut() => new(
        _users.Object, _refreshTokens.Object, _tokenService.Object, _rtGenerator.Object);

    [Fact]
    public async Task Execute_ValidUser_ReturnsToken()
    {
        var user = AppUser.CreateWindowsUser("jeff.wang", ["orders.read"]);
        _users.Setup(r => r.FindByUsernameAsync("jeff.wang", default)).ReturnsAsync(user);
        _tokenService.Setup(s => s.Generate(user)).Returns("fake-at");
        _rtGenerator.Setup(g => g.Generate()).Returns(("raw-rt", "hash"));

        var result = await Sut().ExecuteAsync(@"CORP\jeff.wang");

        result.AccessToken.Should().Be("fake-at");
        result.RefreshToken.Should().Be("raw-rt");
    }

    [Fact]
    public async Task Execute_UserNotFound_Throws()
    {
        _users.Setup(r => r.FindByUsernameAsync(It.IsAny<string>(), default))
              .ReturnsAsync((AppUser?)null);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => Sut().ExecuteAsync("ghost"));
    }

    [Theory]
    [InlineData(@"CORP\jeff.wang", "jeff.wang")]
    [InlineData("JEFF.WANG", "jeff.wang")]
    public async Task Execute_NormalizesUsername(string input, string expected)
    {
        var user = AppUser.CreateWindowsUser(expected, []);
        _users.Setup(r => r.FindByUsernameAsync(expected, default)).ReturnsAsync(user);
        _tokenService.Setup(s => s.Generate(It.IsAny<AppUser>())).Returns("at");
        _rtGenerator.Setup(g => g.Generate()).Returns(("rt", "h"));

        await Sut().ExecuteAsync(input);

        _users.Verify(r => r.FindByUsernameAsync(expected, default), Times.Once);
    }
}
