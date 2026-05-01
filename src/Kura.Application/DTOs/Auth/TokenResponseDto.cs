namespace Kura.Application.DTOs.Auth;

public sealed class TokenResponseDto
{
    public string AccessToken { get; init; } = string.Empty;
    public DateTime ExpiresAt { get; init; }
}
