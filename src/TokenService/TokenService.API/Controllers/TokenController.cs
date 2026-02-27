using Microsoft.AspNetCore.Mvc;
using TokenService.Application.DTOs;
using TokenService.Application.Exceptions;
using TokenService.Application.UseCases;

namespace TokenService.API.Controllers;

[ApiController, Route("api/token")]
public class TokenController(
    IssueTokenUseCase issueToken,
    RefreshTokenUseCase refreshToken) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> Token([FromBody] TokenRequest req, CancellationToken ct)
    {
        if (req.GrantType != "windows_identity")
            return BadRequest(new { error = "unsupported_grant_type" });

        var windowsUser = HttpContext.Items["WindowsUser"] as string
                       ?? HttpContext.User?.Identity?.Name;

        if (string.IsNullOrWhiteSpace(windowsUser))
            return Unauthorized(new { error = "windows_auth_required" });

        try { return Ok(await issueToken.ExecuteAsync(windowsUser, ct)); }
        catch (UnauthorizedAccessException ex)
        { return Unauthorized(new { error = "unauthorized", error_description = ex.Message }); }
    }

    [HttpPost("refresh")]
    public async Task<IActionResult> Refresh([FromBody] RefreshRequest req, CancellationToken ct)
    {
        try { return Ok(await refreshToken.ExecuteAsync(req.RefreshToken, ct)); }
        catch (TokenReuseException)
        { return Unauthorized(new { error = "token_reuse_detected" }); }
        catch (UnauthorizedAccessException ex)
        { return Unauthorized(new { error = ex.Message }); }
        catch (ConcurrencyException)
        { return StatusCode(503, new { error = "concurrent_request" }); }
    }
}
