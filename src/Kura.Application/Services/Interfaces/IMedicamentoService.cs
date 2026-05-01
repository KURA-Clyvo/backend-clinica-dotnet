namespace Kura.Application.Services.Interfaces;

using Kura.Application.DTOs.Medicamento;

public interface IMedicamentoService
{
    Task<IEnumerable<MedicamentoResponseDto>> GetAllAsync();
    Task<MedicamentoResponseDto> GetByIdAsync(long id);
    Task<MedicamentoResponseDto> CreateAsync(MedicamentoCreateDto dto);
    Task SoftDeleteAsync(long id);
}
