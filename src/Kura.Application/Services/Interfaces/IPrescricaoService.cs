namespace Kura.Application.Services.Interfaces;

using Kura.Application.DTOs.Prescricao;

public interface IPrescricaoService
{
    Task<PrescricaoResponseDto> CreateAsync(PrescricaoCreateDto dto);
    Task<PrescricaoResponseDto> GetByEventoClinicoAsync(long idEvento);
    Task<IEnumerable<PrescricaoResponseDto>> GetByPetAsync(long idPet);
}
