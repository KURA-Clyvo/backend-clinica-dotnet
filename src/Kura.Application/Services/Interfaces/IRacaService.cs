namespace Kura.Application.Services.Interfaces;

using Kura.Application.DTOs.Raca;

public interface IRacaService
{
    Task<IEnumerable<RacaResponseDto>> GetAllAsync();
    Task<RacaResponseDto> GetByIdAsync(long id);
    Task<IEnumerable<RacaResponseDto>> GetByEspecieAsync(long idEspecie);
    Task<RacaResponseDto> CreateAsync(RacaCreateDto dto);
    Task SoftDeleteAsync(long id);
}
