namespace Kura.Application.Services.Interfaces;

using Kura.Application.DTOs.DispositivoIot;

public interface IDispositivoIotService
{
    Task<IEnumerable<DispositivoIotResponseDto>> GetByClinicaAsync(long idClinica);
    Task<DispositivoIotResponseDto> GetByIdAsync(long id);
    Task<DispositivoIotResponseDto> CreateAsync(DispositivoIotCreateDto dto);
    Task SoftDeleteAsync(long id);
}
