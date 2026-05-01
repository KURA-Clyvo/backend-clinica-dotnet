namespace Kura.Application.Services.Interfaces;

using Kura.Application.DTOs.Vacina;

public interface IVacinaService
{
    Task<VacinaResponseDto> CreateAsync(VacinaCreateDto dto);
    Task<VacinaResponseDto> GetByEventoClinicoAsync(long idEvento);
    Task<IEnumerable<VacinaResponseDto>> GetByPetAsync(long idPet);
}
