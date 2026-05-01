namespace Kura.Application.Services.Interfaces;

using Kura.Application.DTOs.Auth;

public interface IAuthService
{
    Task<TokenResponseDto> LoginAsync(LoginDto dto);
}
