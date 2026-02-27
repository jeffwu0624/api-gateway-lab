using System.Text.Json.Serialization;

namespace TokenService.Application.DTOs;

public record TokenRequest(
    [property: JsonPropertyName("grant_type")] string GrantType,
    [property: JsonPropertyName("client_id")] string? ClientId);

public record RefreshRequest(
    [property: JsonPropertyName("refresh_token")] string RefreshToken);

public record TokenResponse(
    [property: JsonPropertyName("access_token")] string AccessToken,
    [property: JsonPropertyName("refresh_token")] string RefreshToken,
    [property: JsonPropertyName("token_type")] string TokenType,
    [property: JsonPropertyName("expires_in")] int ExpiresIn,
    [property: JsonPropertyName("scope")] string Scope);
