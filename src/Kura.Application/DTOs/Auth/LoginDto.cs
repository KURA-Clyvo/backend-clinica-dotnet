namespace Kura.Application.DTOs.Auth;

public sealed class LoginDto
{
    public string DsEmail { get; init; } = string.Empty;
    public string DsSenha { get; init; } = string.Empty;
}
