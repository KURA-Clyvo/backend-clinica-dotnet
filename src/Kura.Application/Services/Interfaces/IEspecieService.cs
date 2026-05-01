namespace Kura.Application.Services.Interfaces;

using Kura.Application.DTOs.Especie;

public interface IEspecieService
{
    Task<IEnumerable<EspecieResponseDto>> GetAllAsync();
    Task<EspecieResponseDto> GetByIdAsync(long id);
    Task<EspecieResponseDto> CreateAsync(EspecieCreateDto dto);
    Task SoftDeleteAsync(long id);
}
